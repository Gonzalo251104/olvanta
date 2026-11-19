using System.ComponentModel.DataAnnotations;

namespace Olvanta.Application;

public sealed record ReportFilter(DateOnly? From = null, DateOnly? Through = null, [MaxLength(256)] string? Seller = null, [MaxLength(80)] string? Lot = null);
public sealed record BranchReport(string Title, string Branch, string Currency, string Scope, string[] Columns, List<string[]> Rows);
