using System.ComponentModel.DataAnnotations;
using Olvanta.Domain;

namespace Olvanta.Application;

public sealed record WarehouseInput([Required, MaxLength(100)] string Name);
public sealed record MinimumInput(int ProductId, int WarehouseId, decimal Quantity);
public sealed record PurchaseItem(int ProductId, decimal Quantity, decimal UnitCost);
public sealed record PurchaseInput(int SupplierId, [Required, MaxLength(80)] string Reference,
    [Required, MinLength(1), MaxLength(100)] PurchaseItem[] Lines);
public sealed record MovementInput(Guid OperationId, [Required] string Kind, int WarehouseId, int ProductId,
    decimal Quantity, [Required, MaxLength(240)] string Reason, decimal? UnitCost = null,
    [MaxLength(80)] string? Lot = null, DateOnly? Expiry = null, int? DestinationId = null,
    int? PurchaseLineId = null, long? RelatedId = null)
{
    public string? Validate()
    {
        if (OperationId == Guid.Empty) return "Indique un identificador único para la operación.";
        if (!StockMath.ValidQuantity(Quantity)) return "Indique una cantidad positiva con hasta seis decimales.";
        var needsCost = Kind is "entry" or "adjust-in";
        if (needsCost != UnitCost.HasValue || (UnitCost is decimal cost && !StockMath.ValidCost(cost)))
            return "Indique el costo por unidad solo en entradas directas y ajustes positivos.";
        if ((Kind == "receipt") != PurchaseLineId.HasValue) return "Vincule cada recepción con una partida de compra.";
        if ((Kind == "transfer") != DestinationId.HasValue || DestinationId == WarehouseId)
            return "Para un traslado, seleccione un almacén de destino distinto del origen.";
        if (Expiry.HasValue && (string.IsNullOrWhiteSpace(Lot) || !(needsCost || Kind == "receipt")))
            return "Registre el vencimiento junto al lote en su entrada.";
        if (Kind == "release" && RelatedId == null) return "Seleccione la reserva que desea liberar.";
        if (RelatedId != null && Kind is not ("release" or "adjust-in" or "adjust-out"))
            return "Solo los ajustes y las liberaciones admiten un movimiento de referencia.";
        return null;
    }
}

public sealed record InventoryOption(int Id, string Name);
public sealed record ProductOption(int Id, string Name, string Unit, bool Active);
public sealed record OrderLineView(int Id, int ProductId, decimal Quantity, decimal Received, decimal UnitCost);
public sealed record PurchaseView(int Id, string Reference, string Supplier, string Currency, List<OrderLineView> Lines);
public sealed record LotBalance(string Lot, DateOnly? Expiry, decimal Quantity, decimal Reserved)
{
    public decimal Available => Quantity - Reserved;
}
public sealed record BalanceView(int WarehouseId, int ProductId, decimal Quantity, decimal Reserved,
    decimal Value, decimal Minimum, List<LotBalance> Lots)
{
    public decimal Available => Quantity - Reserved;
    public decimal AverageCost => Quantity == 0 ? 0 : decimal.Round(Value / Quantity, 6, MidpointRounding.AwayFromZero);
    public bool BelowMinimum => Available < Minimum;
}
public sealed record OpenReservation(long Id, int WarehouseId, int ProductId, string Lot, decimal Quantity, string Reason);
public sealed record InventoryView(List<InventoryOption> Warehouses, List<ProductOption> Products,
    List<InventoryOption> Suppliers, List<PurchaseView> Purchases, List<BalanceView> Balances, List<OpenReservation> Reservations);
public sealed record MovementView(long Id, Guid OperationId, string Kind, DateTimeOffset Time, string Actor,
    string Reason, int WarehouseId, int ProductId, string Lot, DateOnly? Expiry, decimal Quantity,
    decimal Reserved, decimal Value, int? PurchaseLineId, long? RelatedId);
