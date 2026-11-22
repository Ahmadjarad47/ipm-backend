using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Identity;

namespace Ipm.Api.Infrastructure;

/// <summary>
/// R6: a user whose password was set by an administrator may only use <c>/api/v1/auth/*</c> (read their profile,
/// change the password, sign out) until they choose their own password. Everything else answers 403.
/// </summary>
internal sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly PathString Api = new("/api/v1");
    private static readonly PathString Auth = new("/api/v1/auth");

    public async Task InvokeAsync(HttpContext context, IAccessService access)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.Request.Path.StartsWithSegments(Api)
            && !context.Request.Path.StartsWithSegments(Auth)
            && Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId)
            && await access.GetAsync(userId, context.RequestAborted) is { MustChangePassword: true })
        {
            await AuthErrors.PasswordChangeRequired.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
