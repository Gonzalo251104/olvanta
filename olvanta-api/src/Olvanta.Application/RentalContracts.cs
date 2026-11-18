using System.ComponentModel.DataAnnotations;
namespace Olvanta.Application;

public sealed record UnitInput([Required, MaxLength(30)] string Code, [Required, MaxLength(100)] string Name, [Required, MaxLength(200)] string Address, [MaxLength(500)] string Features);
public sealed record LeaseInput(int UnitId, int CustomerId, int ServiceId, DateOnly Start, DateOnly End, decimal MonthlyRent, decimal Deposit, int DueDay, [Required, MaxLength(1000)] string Conditions, int? PreviousId = null);
public sealed record EndLeaseInput(DateOnly Date, [Required, MaxLength(200)] string Reason);
public sealed record RentMonthInput(DateOnly Month);
public sealed record ExtraChargeInput([Required, MaxLength(64)] string Reference, [Required, MaxLength(160)] string Reason, DateOnly Due, decimal Amount);
public sealed record DepositInput(Guid RequestId, int SessionId, string Kind, string Method, decimal Amount, [Required, MaxLength(140)] string Note);
public sealed record RentalUnitView(int Id, string Code, string Name, string Address, string Features, bool Occupied);
public sealed record LeaseView(int Id, int UnitId, int CustomerId, int ServiceId, int? PreviousId, string Tenant, string Currency, decimal TaxRate, DateOnly Start, DateOnly End, DateOnly? EndedOn, string? EndReason, int DueDay, decimal MonthlyRent, decimal Deposit, decimal Held, string Conditions);
public sealed record RentChargeView(int Id, int LeaseId, int SaleId, string Kind, string Reference, string Description, DateOnly From, DateOnly Through, DateOnly Due, decimal Amount, decimal Balance);
public sealed record RentalDeskView(DateOnly Today, List<RentalUnitView> Units, List<LeaseView> Contracts, List<RentChargeView> Charges, List<InventoryOption> Customers, List<InventoryOption> Services, int? CashSessionId, string? CashCurrency, List<DepositEntry> Deposits);
public sealed record DepositEntry(long Id, int LeaseId, string Kind, decimal Amount, string Method, string Actor, DateTimeOffset Time, string Note);
