using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Olvanta.Api;

public sealed class DataConflictHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is Olvanta.Application.TradeException business)
        {
            await Results.Problem(statusCode: business.Status, detail: business.Message).ExecuteAsync(context);
            return true;
        }
        var message = exception switch
        {
            DbUpdateConcurrencyException => "El registro cambió. Vuelva a cargarlo antes de guardar.",
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => "Ya existe un registro con ese identificador.",
            _ => null
        };
        if (message == null) return false;
        await Results.Problem(statusCode: 409, detail: message).ExecuteAsync(context);
        return true;
    }
}
