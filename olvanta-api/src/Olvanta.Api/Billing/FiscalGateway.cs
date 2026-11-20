using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Billing;

public sealed record Delivery(string Status, string Detail, string? Ticket = null, byte[]? Cdr = null, byte[]? Response = null);
public interface IFiscalGateway
{
    Task<Delivery> Exchange(FiscalDocument doc, bool query, string scenario);
}

public static class CdrReader
{
    internal static readonly XNamespace ResponseNamespace = "urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2";
    public static Delivery Parse(FiscalDocument doc, byte[] zip)
    {
        var xml = FiscalFiles.ReadXml(FiscalFiles.UnpackCdr(zip, doc.FileStem));
        var root = XElement.Parse(xml.OuterXml);
        var result = root.Element(FiscalXml.A + "DocumentResponse")?.Element(FiscalXml.A + "Response");
        var code = (string?)result?.Element(FiscalXml.C + "ResponseCode");
        if (root.Name != ResponseNamespace + "ApplicationResponse" || result?.Element(FiscalXml.C + "ReferenceID")?.Value != doc.Identifier ||
            string.IsNullOrEmpty(code) || !int.TryParse(code, out _)) throw new InvalidDataException("El CDR no corresponde al comprobante.");
        var message = code + " · " + (string?)result.Element(FiscalXml.C + "Description");
        return new Delivery(code == "0" ? "accepted" : "rejected", message, Cdr: zip, Response: zip);
    }
}

public sealed class SimulatedGateway(OlvantaDbContext db) : IFiscalGateway
{
    public async Task<Delivery> Exchange(FiscalDocument doc, bool query, string scenario)
    {
        var receipt = await db.Set<SimulationReceipt>().FindAsync(doc.Id);
        if (query) return receipt == null ? new Delivery("prepared", "El receptor simulado confirma que no recibió el ZIP.") : CdrReader.Parse(doc, receipt.Cdr);
        if (receipt == null)
        {
            var result = new XElement(CdrReader.ResponseNamespace + "ApplicationResponse", new XAttribute(XNamespace.Xmlns + "cbc", FiscalXml.C),
                new XAttribute(XNamespace.Xmlns + "cac", FiscalXml.A), new XElement(FiscalXml.C + "ID", "SIM-" + doc.Identifier),
                new XElement(FiscalXml.A + "DocumentResponse", new XElement(FiscalXml.A + "Response", new XElement(FiscalXml.C + "ReferenceID", doc.Identifier),
                    new XElement(FiscalXml.C + "ResponseCode", scenario == "reject" ? "2000" : "0"),
                    new XElement(FiscalXml.C + "Description", scenario == "reject" ? "Rechazo simulado" : "Aceptación simulada"))));
            receipt = new SimulationReceipt { DocumentId = doc.Id, Cdr = FiscalFiles.Pack("R-" + doc.FileStem + ".xml", Encoding.UTF8.GetBytes(result.ToString())) };
            db.Add(receipt);
            await db.SaveChangesAsync();
        }
        if (scenario == "lost") return new Delivery("uncertain", "Se perdió la respuesta simulada. Consulte antes de reenviar.");
        return doc.Type is "RA" or "RC" ? new Delivery("processing", "Ticket de simulación recibido.", "SIM-" + doc.Id) : CdrReader.Parse(doc, receipt.Cdr);
    }
}

public sealed class BetaGateway(HttpClient http, IConfiguration config) : IFiscalGateway
{
    public const string Endpoint = "https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Service = "http://service.sunat.gob.pe";
    private static readonly XNamespace Security = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    public async Task<Delivery> Exchange(FiscalDocument doc, bool query, string scenario)
    {
        if (query && string.IsNullOrEmpty(doc.Ticket)) return new Delivery("uncertain", "Sin ticket de BETA. Se requiere conciliación externa antes de otro envío.");
        var ruc = config["Billing:BetaRuc"];
        if (string.IsNullOrEmpty(ruc) || ruc != FiscalIssuer.Read(doc).Ruc) throw new TradeException("Falta el emisor BETA autorizado.", 503);
        var operation = query ? "getStatus" : doc.Type is "RC" or "RA" ? "sendSummary" : "sendBill";
        var body = new XElement(Service + operation);
        if (query) body.Add(new XElement("ticket", doc.Ticket));
        else body.Add(new XElement("fileName", doc.FileStem + ".zip"), new XElement("contentFile", Convert.ToBase64String(doc.Zip)));
        var envelope = new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", Soap), new XAttribute(XNamespace.Xmlns + "wsse", Security),
            new XElement(Soap + "Header", new XElement(Security + "Security", new XElement(Security + "UsernameToken",
                new XElement(Security + "Username", ruc + "MODDATOS"), new XElement(Security + "Password", "MODDATOS")))), new XElement(Soap + "Body", body));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml") };
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsByteArrayAsync();
        // Never persist an unexpected server page or echoed WS-Security credentials.
        var soap = XElement.Parse(FiscalFiles.ReadXml(raw).OuterXml).Element(Soap + "Body");
        var fault = soap?.Element(Soap + "Fault");
        if (fault != null || !response.IsSuccessStatusCode)
            return new Delivery("uncertain", $"BETA HTTP {(int)response.StatusCode}, código SOAP {(string?)fault?.Element("faultcode") ?? "sin código"}. Requiere consulta.");
        var reply = soap?.Elements().SingleOrDefault(x => x.Name.LocalName == operation + "Response")
            ?? throw new InvalidDataException("Respuesta SOAP no reconocida.");
        string? Value(string field) => reply.Descendants().FirstOrDefault(x => x.Name.LocalName == field)?.Value;
        var evidence = Encoding.UTF8.GetBytes(reply.ToString());
        if (operation == "sendSummary" && Value("ticket") is { Length: > 0 and < 100 } ticket)
            return new Delivery("processing", "SUNAT BETA entregó un ticket.", ticket, Response: evidence);
        if (query && Value("statusCode") == "98") return new Delivery("processing", "Ticket en procesamiento.", doc.Ticket, Response: evidence);
        var content = Value(query ? "content" : "applicationResponse");
        if (string.IsNullOrWhiteSpace(content)) return new Delivery("uncertain", "Respuesta sin CDR. Requiere conciliación.", doc.Ticket, Response: evidence);
        var cdr = CdrReader.Parse(doc, Convert.FromBase64String(content));
        return cdr with { Response = evidence };
    }
}
