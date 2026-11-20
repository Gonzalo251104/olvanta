using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/inventory/movements")]
public sealed class StockMovementsController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    [HttpPost, ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Post(int branchId, MovementInput input)
    {
        var permission = input.Kind switch
        {
            "receipt" or "entry" or "issue" or "transfer" => AccessRules.InventoryWrite,
            "reserve" or "release" => AccessRules.Reserve,
            "adjust-in" or "adjust-out" => AccessRules.InventoryAdjust,
            _ => null
        };
        if (permission == null) return Problem(statusCode: 400, detail: "Tipo de movimiento desconocido.");
        if (!await access.AllowsAsync(branchId, permission)) return Forbid();
        if (input.Validate() is string error) return Problem(statusCode: 400, detail: error);
        var lot = input.Lot?.Trim().ToUpperInvariant() ?? "";

        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        var ledger = db.Movements.Where(x => x.BranchId == branchId);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        var previous = await ledger.Where(x => x.OperationId == input.OperationId).Select(x => x.RequestHash).FirstOrDefaultAsync();
        if (previous != null) return previous == hash ? NoContent() : ConflictWith("El identificador de operación ya tiene otro contenido.");

        var warehouses = db.Warehouses.Where(x => x.BranchId == branchId);
        var product = await db.Masters.SingleOrDefaultAsync(x => x.BranchId == branchId && x.Kind == "products" && x.Id == input.ProductId);
        if (product == null || !await warehouses.AnyAsync(x => x.Id == input.WarehouseId) ||
            (input.DestinationId is int dest && !await warehouses.AnyAsync(x => x.Id == dest))) return NotFound();
        if (!product.Active && input.Kind is ("entry" or "reserve")) return ConflictWith("El producto está inactivo.");

        var forProduct = ledger.Where(x => x.ProductId == input.ProductId);
        var olive = await db.OliveLots.SingleOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == product.Id && x.Code == lot);
        if (olive != null && (input.Kind is "entry" or "receipt" or "adjust-in"))
            return ConflictWith("Este lote pesado tiene un origen registrado. Use un lote nuevo para otra recepción.");
        if (olive != null && !OliveUnits.Valid(olive, input.Quantity))
            return ConflictWith("Las presentaciones de este lote se mueven en unidades enteras.");
        var lotInfo = await forProduct.Where(x => x.Lot == lot).Select(x => new { x.Expiry }).FirstOrDefaultAsync();
        if (input.Olive != null && (!OliveUnits.IsBulk(product.Unit!) || product.Conversion != 1 || lotInfo != null || product.Currency != branch!.Currency))
            return ConflictWith("La recepción pesada requiere un producto a granel en kg, factor 1, moneda de la sucursal y un lote interno nuevo.");
        if (lotInfo != null && input.Expiry.HasValue && input.Expiry != lotInfo.Expiry)
            return ConflictWith("El lote ya tiene otro vencimiento registrado.");
        var expiry = lotInfo == null ? input.Expiry : lotInfo.Expiry;
        var atWarehouse = forProduct.Where(x => x.WarehouseId == input.WarehouseId);
        var totals = await atWarehouse.GroupBy(x => x.ProductId).Select(g => new
        {
            OnHand = g.Sum(x => x.Quantity),
            Value = g.Sum(x => x.Value),
            Available = g.Sum(x => x.Lot == lot ? x.Quantity - x.Reserved : 0)
        }).SingleOrDefaultAsync();
        var available = totals?.Available ?? 0;
        var quantity = input.Quantity;
        if (quantity > available && input.Kind is ("issue" or "adjust-out" or "transfer" or "reserve"))
            return ConflictWith("La cantidad supera el disponible de este lote y almacén.");

        var move = new StockMovement
        {
            BranchId = branchId,
            WarehouseId = input.WarehouseId,
            ProductId = input.ProductId,
            OperationId = input.OperationId,
            RequestHash = hash,
            Kind = input.Kind,
            Actor = access.Actor,
            Reason = input.Reason.Trim(),
            Lot = lot,
            Expiry = expiry,
            PurchaseLineId = input.PurchaseLineId,
            RelatedId = input.RelatedId
        };
        if (input.RelatedId is long relatedId)
        {
            var original = await atWarehouse.FirstOrDefaultAsync(x => x.Id == relatedId && x.Lot == lot);
            if (original == null) return NotFound();
            if (input.Kind == "release" && (original.Kind != "reserve" || quantity != original.Reserved ||
                await ledger.AnyAsync(x => x.RelatedId == relatedId && x.Kind == "release")))
                return ConflictWith("La reserva ya se liberó o la cantidad no corresponde.");
            if ((input.Kind == "adjust-in" && original.Quantity >= 0) || (input.Kind == "adjust-out" && original.Quantity <= 0))
                return ConflictWith("La compensación debe tener sentido opuesto al movimiento original.");
        }
        switch (input.Kind)
        {
            case "receipt":
                var line = await (from l in db.PurchaseLines
                                  join p in db.Purchases on l.PurchaseId equals p.Id
                                  where p.BranchId == branchId && l.Id == input.PurchaseLineId && l.ProductId == input.ProductId
                                  select l).SingleOrDefaultAsync();
                if (line == null) return NotFound();
                if (quantity > line.Quantity - line.Received) return ConflictWith("La recepción supera lo pendiente de la compra.");
                move.Quantity = quantity;
                move.Value = StockMath.PartialReceiptValue(line.Received, quantity, line.UnitCost);
                line.Received += quantity;
                if (input.Olive is OliveWeights weights)
                {
                    var supplier = await (from purchase in db.Purchases
                                          join s in db.Masters on purchase.SupplierId equals s.Id
                                          where purchase.Id == line.PurchaseId
                                          select s.Name + " · " + s.Code).SingleAsync();
                    db.OliveLots.Add(new OliveLot
                    {
                        BranchId = branchId,
                        ProductId = product.Id,
                        Code = lot,
                        Product = product.Name,
                        Unit = product.Unit!,
                        Presentation = product.Presentation ?? "Granel",
                        KgPerUnit = 1,
                        Receipt = move,
                        Supplier = supplier,
                        SupplierLot = weights.SupplierLot.Trim(),
                        Origin = weights.Origin.Trim(),
                        Variety = weights.Variety.Trim(),
                        Grade = weights.Grade.Trim(),
                        Gross = weights.Gross,
                        Tare = weights.Tare
                    });
                }
                break;
            case "entry":
            case "adjust-in":
                move.Quantity = quantity;
                move.Value = StockMath.ReceiptValue(quantity, input.UnitCost!.Value);
                break;
            case "reserve":
                move.Reserved = quantity;
                break;
            case "release":
                move.Reserved = -quantity;
                break;
            default:
                move.Quantity = -quantity;
                move.Value = -StockMath.IssueValue(quantity, totals!.OnHand, totals.Value);
                break;
        }
        db.Movements.Add(move);
        if (input.DestinationId is int destination)
            db.Movements.Add(new StockMovement
            {
                BranchId = branchId,
                WarehouseId = destination,
                ProductId = move.ProductId,
                OperationId = move.OperationId,
                RequestHash = hash,
                Kind = "transfer-in",
                Actor = move.Actor,
                Reason = move.Reason,
                Lot = lot,
                Expiry = expiry,
                Quantity = quantity,
                Value = -move.Value,
                Time = move.Time
            });
        access.Record("stock." + input.Kind, $"{move.OperationId} · {product.Code} · {quantity} · {move.Reason}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private ObjectResult ConflictWith(string message) => Problem(statusCode: 409, detail: message);
}
