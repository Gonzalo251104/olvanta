using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Olvanta.Application;

namespace Olvanta.Api.Billing;

public sealed class FiscalSigner(IConfiguration config) : IDisposable
{
    private readonly Lazy<X509Certificate2> certificate = new(() => Load(config));
    public string Mode => config["Billing:Mode"] switch
    {
        null or "" or "simulation" => "simulation",
        "beta" => "beta",
        _ => throw new TradeException("El entorno de facturación configurado no es válido.", 503)
    };
    public (byte[] Xml, string Digest) Sign(XDocument source, string type)
    {
        var doc = FiscalFiles.ReadXml(Encoding.UTF8.GetBytes(source.ToString(SaveOptions.DisableFormatting)));
        var cert = certificate.Value;
        using var key = cert.GetRSAPrivateKey() ?? throw new TradeException("El certificado requiere una clave RSA privada.", 503);
        var signed = new SignedXml(doc) { SigningKey = key };
        signed.Signature.Id = "SignatureOlvanta";
        signed.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        var reference = new Reference("") { DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signed.AddReference(reference);
        signed.KeyInfo = new KeyInfo();
        signed.KeyInfo.AddClause(new KeyInfoX509Data(cert));
        signed.ComputeSignature();
        doc.GetElementsByTagName("ExtensionContent", FiscalXml.E.NamespaceName)[0]!.AppendChild(doc.ImportNode(signed.GetXml(), true));
        var result = Encoding.UTF8.GetBytes(doc.OuterXml);
        var parsed = FiscalFiles.ReadXml(result);
        var verifier = new SignedXml(parsed);
        verifier.LoadXml((XmlElement)parsed.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!);
        if (!verifier.CheckSignature(cert, true)) throw new InvalidOperationException("Falló la verificación de la firma.");
        if (type is not ("RC" or "RA")) FiscalFiles.ValidateUbl(parsed);
        return (result, Convert.ToBase64String(reference.DigestValue!));
    }

    private static X509Certificate2 Load(IConfiguration config)
    {
        if (config["Billing:Mode"] == "beta" && !string.IsNullOrWhiteSpace(config["Billing:CertificatePath"]))
        {
            var path = config["Billing:CertificatePath"];
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new TradeException("Configure el certificado de pruebas en el backend.", 503);
            try
            {
                var cert = X509CertificateLoader.LoadPkcs12FromFile(path, config["Billing:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
                if (!cert.HasPrivateKey || DateTime.UtcNow < cert.NotBefore.ToUniversalTime() || DateTime.UtcNow > cert.NotAfter.ToUniversalTime())
                { cert.Dispose(); throw new TradeException("Revise la vigencia y la clave privada del certificado de pruebas.", 503); }
                return cert;
            }
            catch (CryptographicException) { throw new TradeException("No se pudo abrir el certificado de pruebas.", 503); }
        }
        using var rsa = RSA.Create(2048);
        var subject = config["Billing:Mode"] == "beta" ? "CN=Olvanta PRUEBAS BETA" : "CN=Olvanta SIMULACION LOCAL";
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var temporary = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(temporary.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.EphemeralKeySet);
    }
    public void Dispose() { if (certificate.IsValueCreated) certificate.Value.Dispose(); }
}

public static class FiscalFiles
{
    public static XmlDocument ReadXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        doc.Load(reader);
        return doc;
    }

    public static byte[] Pack(string name, byte[] content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var file = archive.CreateEntry(name, CompressionLevel.Optimal).Open()) file.Write(content);
        return stream.ToArray();
    }

    internal static byte[] UnpackCdr(byte[] zip, string fileStem)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        if (archive.Entries.Count != 1) throw new InvalidDataException("El CDR debe contener un solo XML.");
        var file = archive.Entries[0];
        if (!string.Equals(file.FullName, "R-" + fileStem + ".xml", StringComparison.OrdinalIgnoreCase) || file.Length > 2_000_000)
            throw new InvalidDataException("Nombre o tamaño inesperado en el CDR.");
        using var source = file.Open();
        var bytes = new byte[checked((int)file.Length)];
        source.ReadExactly(bytes);
        if (source.ReadByte() != -1) throw new InvalidDataException("CDR demasiado grande.");
        return bytes;
    }

    private static readonly Lazy<XmlSchemaSet> Schemas = new(() =>
    {
        using var stream = typeof(FiscalFiles).Assembly.GetManifestResourceStream("Olvanta.Api.Billing.Schemas.ubl-2.1.zip")!;
        using var archive = new ZipArchive(stream);
        var resolver = new BundledSchemas(archive.Entries.ToDictionary(x => x.FullName, x =>
        {
            using var input = x.Open(); using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
        }));
        var schemas = new XmlSchemaSet { XmlResolver = resolver };
        foreach (var name in new[] { "Invoice", "CreditNote", "DebitNote" })
        {
            var uri = new Uri($"https://schemas.invalid/xsd/maindoc/UBL-{name}-2.1.xsd");
            using var reader = XmlReader.Create((Stream)resolver.GetEntity(uri, null, null), new XmlReaderSettings { XmlResolver = null }, uri.AbsoluteUri);
            schemas.Add(null, reader);
        }
        schemas.Compile();
        return schemas;
    });
    public static void ValidateUbl(XmlDocument doc) { doc.Schemas = Schemas.Value; doc.Validate(null); }

    private sealed class BundledSchemas(Dictionary<string, byte[]> files) : XmlResolver
    {
        public override ICredentials? Credentials { set { } }
        public override object GetEntity(Uri uri, string? role, Type? returnType) =>
            uri.Host == "schemas.invalid" && files.TryGetValue(uri.AbsolutePath.TrimStart('/'), out var bytes)
                ? new MemoryStream(bytes, false) : throw new XmlException("Esquema externo no permitido.");
    }
}
