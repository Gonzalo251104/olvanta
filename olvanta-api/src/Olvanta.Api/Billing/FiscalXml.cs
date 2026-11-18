using System.Globalization;
using System.Xml.Linq;
using Olvanta.Application;

namespace Olvanta.Api.Billing;

public static class FiscalXml
{
    internal static readonly XNamespace C = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    internal static readonly XNamespace A = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    internal static readonly XNamespace E = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    internal static readonly XNamespace S = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    private static XElement Basic(string name, object value) => new(C + name, value);
    private static XElement Aggregate(string name, params object?[] content) => new(A + name, content);
    internal static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Price(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);
    private static XElement Party(string name, string type, string number, FiscalSnapshot? issuer = null) =>
        Aggregate("Party", Aggregate("PartyIdentification", new XElement(C + "ID", new XAttribute("schemeID", type), number)),
            Aggregate("PartyLegalEntity", Basic("RegistrationName", name), issuer == null ? null :
                Aggregate("RegistrationAddress", Basic("AddressTypeCode", issuer.SiteCode), Aggregate("AddressLine", Basic("Line", issuer.Address)),
                    Aggregate("Country", Basic("IdentificationCode", "PE")))));

    public static XDocument Build(FiscalSnapshot s, string type, string id)
    {
        var batch = type is "RC" or "RA";
        var rootName = type switch
        {
            "01" or "03" => "Invoice",
            "07" => "CreditNote",
            "08" => "DebitNote",
            "RC" => "SummaryDocuments",
            "RA" => "VoidedDocuments",
            _ => throw new ArgumentException("Tipo desconocido.")
        };
        XNamespace ns = batch ? $"urn:sunat:names:specification:ubl:peru:schema:xsd:{rootName}-1" :
            $"urn:oasis:names:specification:ubl:schema:xsd:{rootName}-2";
        var root = new XElement(ns + rootName, new XAttribute(XNamespace.Xmlns + "cbc", C), new XAttribute(XNamespace.Xmlns + "cac", A),
            new XAttribute(XNamespace.Xmlns + "ext", E), new XAttribute(XNamespace.Xmlns + "sac", S),
            new XElement(E + "UBLExtensions", new XElement(E + "UBLExtension", new XElement(E + "ExtensionContent"))),
            Basic("UBLVersionID", batch ? "2.0" : "2.1"), Basic("CustomizationID", type switch { "RC" => "1.1", "RA" => "1.0", _ => "2.0" }),
            Basic("ID", id), batch ? Basic("ReferenceDate", s.ReferenceDate!.Value.ToString("yyyy-MM-dd")) : null,
            Basic("IssueDate", s.Date.ToString("yyyy-MM-dd")));
        XElement Money(string name, decimal value) => new(C + name, new XAttribute("currencyID", s.Currency), Amount(value));
        XElement Scheme(string code) => Aggregate("TaxScheme", Basic("ID", code switch { "10" => "1000", "20" => "9997", _ => "9998" }),
            Basic("Name", code switch { "10" => "IGV", "20" => "EXO", _ => "INA" }), Basic("TaxTypeCode", code == "30" ? "FRE" : "VAT"));
        XElement Taxes(IEnumerable<FiscalLine> lines, bool detail = false) =>
            Aggregate("TaxTotal", Money("TaxAmount", lines.Sum(l => l.Tax)), lines.GroupBy(l => (l.Affectation, l.Rate)).Select(g =>
                Aggregate("TaxSubtotal", Money("TaxableAmount", g.Sum(l => l.Total - l.Tax)), Money("TaxAmount", g.Sum(l => l.Tax)),
                    Aggregate("TaxCategory", detail ? Basic("Percent", g.Key.Rate) : null, detail ? Basic("TaxExemptionReasonCode", g.Key.Affectation) : null, Scheme(g.Key.Affectation)))));
        var signature = Aggregate("Signature", Basic("ID", "SignatureOlvanta"), Aggregate("SignatoryParty",
            Aggregate("PartyIdentification", Basic("ID", s.Ruc)), Aggregate("PartyName", Basic("Name", s.Issuer))),
            Aggregate("DigitalSignatureAttachment", Aggregate("ExternalReference", Basic("URI", "#SignatureOlvanta"))));
        if (batch)
        {
            root.Add(signature, Aggregate("AccountingSupplierParty", Basic("CustomerAssignedAccountID", s.Ruc), Basic("AdditionalAccountID", "6"),
                Aggregate("Party", Aggregate("PartyLegalEntity", Basic("RegistrationName", s.Issuer)))));
            var reference = s.Reference!.Split('-');
            if (type == "RA")
                root.Add(new XElement(S + "VoidedDocumentsLine", Basic("LineID", 1), Basic("DocumentTypeCode", s.ReferenceType!),
                    new XElement(S + "DocumentSerialID", reference[0]), new XElement(S + "DocumentNumberID", reference[1]), new XElement(S + "VoidReasonDescription", s.Reason!)));
            else
                root.Add(new XElement(S + "SummaryDocumentsLine", Basic("LineID", 1), Basic("DocumentTypeCode", s.ReferenceType!), Basic("ID", s.Reference),
                    Aggregate("AccountingCustomerParty", Basic("CustomerAssignedAccountID", s.CustomerId), Basic("AdditionalAccountID", s.CustomerType)),
                    s.ReferenceType is "07" or "08" ? Aggregate("BillingReference", Aggregate("InvoiceDocumentReference",
                        Basic("ID", s.AffectedNumber!), Basic("DocumentTypeCode", s.AffectedType!))) : null,
                    Aggregate("Status", Basic("ConditionCode", s.ReasonCode == "void" ? "3" : "1")),
                    new XElement(S + "TotalAmount", new XAttribute("currencyID", s.Currency), Amount(s.Total)),
                    s.Lines.GroupBy(l => l.Affectation).Select(g => new XElement(S + "BillingPayment", Money("PaidAmount", g.Sum(l => l.Total - l.Tax)),
                        Basic("InstructionID", g.Key switch { "10" => "01", "20" => "02", _ => "03" }))),
                    Aggregate("TaxTotal", Money("TaxAmount", s.Tax), s.Lines.GroupBy(l => l.Affectation).Select(g =>
                        Aggregate("TaxSubtotal", Money("TaxAmount", g.Sum(l => l.Tax)), Aggregate("TaxCategory", Scheme(g.Key)))))));
            return new XDocument(root);
        }
        var invoice = rootName == "Invoice";
        root.Add(Basic("IssueTime", s.Time));
        if (invoice) root.Add(new XElement(C + "InvoiceTypeCode", new XAttribute("listID", "0101"), type));
        root.Add(new XElement(C + "Note", new XAttribute("languageLocaleID", "1000"), FiscalLegend.Total(s.Total, s.Currency)), Basic("DocumentCurrencyCode", s.Currency));
        if (!invoice)
            root.Add(Aggregate("DiscrepancyResponse", Basic("ReferenceID", s.Reference!), Basic("ResponseCode", s.ReasonCode!), Basic("Description", s.Reason!)),
                Aggregate("BillingReference", Aggregate("InvoiceDocumentReference", Basic("ID", s.Reference!), Basic("DocumentTypeCode", s.ReferenceType!))));
        root.Add(signature, Aggregate("AccountingSupplierParty", Party(s.Issuer, "6", s.Ruc, s)),
            Aggregate("AccountingCustomerParty", Party(s.Customer, s.CustomerType, s.CustomerId)));
        if (type == "01")
        {
            bool credit = s.Terms == "credit" && s.Outstanding > 0;
            root.Add(Aggregate("PaymentTerms", Basic("ID", "FormaPago"), Basic("PaymentMeansID", credit ? "Credito" : "Contado"),
                credit ? Money("Amount", s.Outstanding) : null));
            if (credit) root.Add(Aggregate("PaymentTerms", Basic("ID", "FormaPago"), Basic("PaymentMeansID", "Cuota001"),
                Money("Amount", s.Outstanding), Basic("PaymentDueDate", s.Due!.Value.ToString("yyyy-MM-dd"))));
        }
        root.Add(Taxes(s.Lines), Aggregate(type == "08" ? "RequestedMonetaryTotal" : "LegalMonetaryTotal",
            Money("LineExtensionAmount", s.Total - s.Tax), Money("TaxInclusiveAmount", s.Total), Money("PayableAmount", s.Total)));
        var qtyTag = type switch { "07" => "CreditedQuantity", "08" => "DebitedQuantity", _ => "InvoicedQuantity" };
        root.Add(s.Lines.Select((l, i) => Aggregate(rootName + "Line", Basic("ID", i + 1),
            new XElement(C + qtyTag, new XAttribute("unitCode", l.Unit), Price(l.Quantity)), Money("LineExtensionAmount", l.Total - l.Tax),
            Aggregate("PricingReference", Aggregate("AlternativeConditionPrice", new XElement(C + "PriceAmount", new XAttribute("currencyID", s.Currency),
                Price(l.Total / l.Quantity)), Basic("PriceTypeCode", "01"))), Taxes([l], true), Aggregate("Item", Basic("Description", l.Name)),
            Aggregate("Price", new XElement(C + "PriceAmount", new XAttribute("currencyID", s.Currency), Price((l.Total - l.Tax) / l.Quantity))))));
        return new XDocument(root);
    }
}
