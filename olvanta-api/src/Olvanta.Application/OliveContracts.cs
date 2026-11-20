using System.ComponentModel.DataAnnotations;

namespace Olvanta.Application;

public sealed record OliveWeights(
    [Required, MaxLength(80)] string Variety, [Required, MaxLength(80)] string Grade,
    [Required, MaxLength(160)] string Origin, [Required, MaxLength(80)] string SupplierLot,
    decimal Gross, decimal Tare);
public sealed record OliveOutput(int ProductId, [Required, MaxLength(80)] string Code, decimal Quantity);
public sealed record OliveRepackInput(Guid OperationId, int WarehouseId, int SourceId, decimal Quantity, decimal LossKg,
    [Required, MaxLength(240)] string Reason, [Required, MaxLength(30)] OliveOutput[] Outputs);

public sealed record OliveProduct(int Id, string Name, string Unit, string Presentation, decimal KgPerUnit);
public sealed record OliveLotView(int Id, int ProductId, string Product, string Code, string Unit, string Presentation, decimal KgPerUnit, bool WholeUnits,
    int? RootId, int? ProcessId, string? Variety, string? Grade, string? Origin, string? Supplier, string? SupplierLot,
    decimal? Gross, decimal? Tare, decimal? Net, DateOnly? Expiry);
public sealed record OliveStock(int LotId, int WarehouseId, decimal OnHand, decimal Available, decimal Value);
public sealed record OliveDesk(List<OliveLotView> Lots, List<OliveStock> Stock, List<OliveProduct> Products, List<InventoryOption> Warehouses, List<InventoryOption> Customers);
public sealed record OlivePurchase(int OrderId, int LineId, string Reference, long ReceiptId, DateTimeOffset Time, decimal Value);
public sealed record OliveProcessView(int Id, int SourceId, DateTimeOffset Time, string Actor, string Reason, decimal Quantity, decimal InputKg, decimal LossKg, decimal LossValue);
public sealed record OliveProcessOutput(int ProcessId, int LotId, decimal Quantity, decimal Kg, decimal Value);
public sealed record OliveDelivery(int LotId, int SaleId, string Customer, int CustomerId, long MovementId, DateTimeOffset Time, string Kind, decimal Quantity, decimal Kg);
public sealed record OliveTrace(int RootId, OlivePurchase Receipt, List<OliveLotView> Lots, List<OliveProcessView> Repacking, List<OliveProcessOutput> Outputs, List<OliveDelivery> Deliveries);
