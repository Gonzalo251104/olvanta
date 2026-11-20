using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Olvanta.Api.Billing;
using Olvanta.Application;
using Xunit;

namespace Olvanta.FoundationTests;

public class FiscalFilesTests
{
    private static readonly FiscalSnapshot Snapshot = new("Olvanta pruebas", "20000000001", "Calle Oliva 12", "0000",
        "Cliente de pruebas", "6", "20000000001", "PEN", new DateOnly(2026, 11, 18), "14:30:00", "credit",
        new DateOnly(2026, 12, 18), 118, "F001-1", "01", new DateOnly(2026, 11, 17), "Ajuste de prueba", "07",
        [new FiscalLine(1, "Aceitunas de mesa", "KGM", 2, 59, 118, 18, 18, "10")]);

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("07")]
    [InlineData("08")]
    [InlineData("RC")]
    [InlineData("RA")]
    public void SignedDocumentsRoundTripWithTheirEmbeddedPublicKey(string type)
    {
        using var signer = new FiscalSigner(new ConfigurationBuilder().Build());
        var (xml, _) = signer.Sign(FiscalXml.Build(Snapshot, type, "F001-2"), type);
        bool Valid(byte[] data)
        {
            var doc = FiscalFiles.ReadXml(data);
            var signature = new SignedXml(doc);
            signature.LoadXml((XmlElement)doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!);
            return signature.CheckSignature();
        }
        Assert.True(Valid(xml));
        Assert.False(Valid(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(xml).Replace("Olvanta pruebas", "Otro emisor"))));
    }
}
