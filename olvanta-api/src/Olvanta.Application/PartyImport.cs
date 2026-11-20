using System.ComponentModel.DataAnnotations;
using Microsoft.VisualBasic.FileIO;

namespace Olvanta.Application;

public sealed record PartyCsvInput([Required, MaxLength(512000)] string Content, bool Commit);
public sealed record CsvIssue(long Row, string Message);
public sealed record CsvParty(long Row, string Code, string Name);
public sealed record PartyCsvResult(List<CsvParty> Rows, List<CsvIssue> Issues, int Imported);

public static class PartyImport
{
    private static readonly string[] Required = ["code", "name", "personType", "documentType"];
    private static readonly string[] Allowed = [.. Required, "active", "address", "contact", "terms"];

    public static (List<(long Row, MasterInput Data)> Rows, List<CsvIssue> Issues) Parse(string text, string kind)
    {
        List<(long, MasterInput)> rows = [];
        List<CsvIssue> issues = [];
        using var parser = new TextFieldParser(new StringReader(text.TrimStart('\uFEFF')));
        using var firstLine = new StringReader(text);
        parser.SetDelimiters(firstLine.ReadLine()?.Contains(';') == true ? ";" : ",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;
        try
        {
            var header = parser.ReadFields();
            if (header == null || header.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Length ||
                Required.Any(h => !header.Contains(h, StringComparer.OrdinalIgnoreCase)) ||
                header.Any(h => !Allowed.Contains(h, StringComparer.OrdinalIgnoreCase)))
                return (rows, [new(1, "Use los encabezados code, name, personType y documentType. Las columnas opcionales son active, address, contact y terms.")]);

            var indexes = header.Select((name, index) => (name, index)).ToDictionary(h => h.name, h => h.index, StringComparer.OrdinalIgnoreCase);
            int record = 2;
            while (!parser.EndOfData)
            {
                if (record > 201) return (rows, [new(record, "El archivo admite hasta 200 registros.")]);
                string[] fields;
                try { fields = parser.ReadFields()!; }
                catch (MalformedLineException)
                {
                    issues.Add(new(record++, $"Formato CSV inválido en la línea física {parser.ErrorLineNumber}."));
                    continue;
                }
                long row = record++;
                if (fields.Length != header.Length)
                {
                    issues.Add(new(row, "La cantidad de columnas difiere del encabezado."));
                    continue;
                }
                string? Field(string name) => indexes.TryGetValue(name, out var index) ? fields[index] : null;
                string? active = Field("active");
                if (!bool.TryParse(active, out var enabled) && !string.IsNullOrEmpty(active) && active is not ("1" or "0"))
                {
                    issues.Add(new(row, "active debe ser true, false, 1 o 0."));
                    continue;
                }
                var data = new MasterInput(Field("name")!, Field("code")!, string.IsNullOrEmpty(active) || active == "1" || enabled,
                    Field("personType"), Field("documentType"), Field("address"), Field("contact"), Field("terms"));
                var validation = new List<ValidationResult>();
                Validator.TryValidateObject(data, new ValidationContext(data), validation, true);
                var error = data.ValidateFor(kind);
                foreach (var invalid in validation)
                    issues.Add(new(row, $"{string.Join(", ", invalid.MemberNames)} requiere un valor dentro del límite permitido."));
                if (error != null) issues.Add(new(row, error));
                if (validation.Count == 0 && error == null) rows.Add((row, data));
            }
        }
        catch (MalformedLineException) { issues.Add(new(1, "El encabezado CSV tiene comillas o separadores inválidos.")); }
        if (rows.Count == 0 && issues.Count == 0) issues.Add(new(2, "El archivo no contiene registros."));
        return (rows, issues);
    }
}
