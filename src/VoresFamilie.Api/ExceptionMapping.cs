using VoresFamilie.Application;
using VoresFamilie.Domain;

namespace VoresFamilie.Api;

public static class ExceptionMapping
{
    public static IApplicationBuilder UseExceptionMapping(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        try { await next(ctx); }
        catch (Exception ex) when (ex is DomainException or NotFoundException or ForbiddenException or ConflictException)
        {
            if (ctx.Response.HasStarted) throw;
            ctx.Response.StatusCode = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ForbiddenException => StatusCodes.Status403Forbidden,
                ConflictException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            await ctx.Response.WriteAsJsonAsync(new ErrorDto(ex.Message));
        }
    });
}

public sealed record ErrorDto(string Message);

