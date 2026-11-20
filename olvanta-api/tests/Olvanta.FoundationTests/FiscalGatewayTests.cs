using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Olvanta.Api.Billing;
using Olvanta.Application;
using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class FiscalGatewayTests
{
    private const string Ruc = "20000000001";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Service = "http://service.sunat.gob.pe";
    private static readonly XNamespace Basic = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Aggregate = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    private static FiscalDocument Example(string type)
    {
        var identifier = type == "RC" ? "RC-20261118-1" : "F001-1";
        return new FiscalDocument
        {
            Type = type,
            Series = "",
            Identifier = identifier,
            FileStem = Ruc + "-" + (type == "RC" ? "" : type + "-") + identifier,
            RequestHash = "",
            Mode = "beta",
            SourceKey = "",
            Status = "prepared",
            Zip = [1, 2, 3],
            Xml = [],
            Pdf = [],
            Digest = "",
            Snapshot = JsonSerializer.Serialize(new FiscalSnapshot("Emisor", Ruc, "", "0000", "Cliente", "6", Ruc, "PEN",
                new DateOnly(2026, 11, 18), "12:00:00", "cash", null, 0, null, null, null, null, null, []))
        };
    }

    private static byte[] Cdr(FiscalDocument doc, string reference)
    {
        XNamespace app = "urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2";
        var xml = new XElement(app + "ApplicationResponse", new XElement(Aggregate + "DocumentResponse", new XElement(Aggregate + "Response",
            new XElement(Basic + "ReferenceID", reference), new XElement(Basic + "ResponseCode", "0"), new XElement(Basic + "Description", "Prueba"))));
        return FiscalFiles.Pack("R-" + doc.FileStem + ".xml", Encoding.UTF8.GetBytes(xml.ToString()));
    }

    private sealed class SoapHandler(Func<XElement, XElement> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(BetaGateway.Endpoint, request.RequestUri!.AbsoluteUri);
            var envelope = XElement.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(Ruc + "MODDATOS", envelope.Descendants().Single(x => x.Name.LocalName == "Username").Value);
            var operation = envelope.Element(Soap + "Body")!.Elements().Single();
            var reply = new XElement(Soap + "Envelope", new XElement(Soap + "Body", respond(operation)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply.ToString()) };
        }
    }

    [Theory]
    [InlineData("01")]
    [InlineData("RC")]
    public async Task TransportUsesTheOriginalZipAndReconcilesTickets(string type)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:BetaRuc"] = Ruc }).Build();
        var doc = Example(type);
        var cdr = Cdr(doc, doc.Identifier);
        using var http = new HttpClient(new SoapHandler(op =>
        {
            if (op.Name == Service + "getStatus")
            {
                Assert.Equal("T123", op.Element("ticket")!.Value);
                return new XElement(Service + "getStatusResponse", new XElement("status", new XElement("statusCode", "0"), new XElement("content", Convert.ToBase64String(cdr))));
            }
            Assert.Equal(doc.FileStem + ".zip", op.Element("fileName")!.Value);
            Assert.Equal(doc.Zip, Convert.FromBase64String(op.Element("contentFile")!.Value));
            Assert.Equal(Service + (type == "RC" ? "sendSummary" : "sendBill"), op.Name);
            return type == "RC" ? new XElement(Service + "sendSummaryResponse", new XElement("ticket", "T123"))
                : new XElement(Service + "sendBillResponse", new XElement("applicationResponse", Convert.ToBase64String(cdr)));
        }));
        var gateway = new BetaGateway(http, config);
        Assert.Equal("uncertain", (await gateway.Exchange(doc, true, "accept")).Status);
        var result = await gateway.Exchange(doc, false, "accept");
        if (type == "RC")
        {
            Assert.Equal("processing", result.Status);
            doc.Ticket = result.Ticket;
            result = await gateway.Exchange(doc, true, "accept");
        }
        Assert.Equal("accepted", result.Status);
        Assert.Equal(cdr, result.Cdr);
        Assert.Throws<InvalidDataException>(() => CdrReader.Parse(doc, Cdr(doc, "OTRO-1")));
    }
}
