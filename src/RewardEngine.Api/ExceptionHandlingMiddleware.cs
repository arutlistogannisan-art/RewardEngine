using System.Text.Json;
using RewardEngine.Application;
namespace RewardEngine.Api;
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            await WriteError(context, 409, "Conflict", "A record with this unique key already exists.");
        }
        catch (AppException ex) { await WriteError(context, ex.StatusCode, ex.Code, ex.Message); }
        catch (BadHttpRequestException ex) { await WriteError(context, 400, "BadRequest", ex.Message); }
        catch (Exception ex) { logger.LogError(ex, "Unhandled request error"); await WriteError(context, 500, "InternalServerError", "An unexpected error occurred."); }
    }
    private static async Task WriteError(HttpContext context, int status, string error, string message)
    {
        if (context.Response.HasStarted)
            return;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error,
            message
        }));
    }
}
