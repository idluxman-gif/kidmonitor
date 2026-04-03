using Microsoft.Extensions.Options;

namespace KidMonitor.Service.Dashboard;

/// <summary>
/// Middleware that enforces PIN-based session authentication for all dashboard API routes.
/// Exempt routes: GET /api/health, POST /api/auth/login.
/// </summary>
public class PinAuthMiddleware
{
    internal const string SessionKey = "dashboard_authed";

    private static readonly HashSet<string> _exemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health",
        "/api/auth/login",
        "/api/auth/setup",
    };

    private readonly RequestDelegate _next;

    public PinAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptionsSnapshot<DashboardOptions> options)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only protect /api/* routes
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Exempt routes skip the check
        if (_exemptPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // Require valid session
        var authed = context.Session.GetString(SessionKey);
        if (authed != "1")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. POST /api/auth/login with the dashboard PIN first." });
            return;
        }

        await _next(context);
    }

    /// <summary>Marks the current session as authenticated.</summary>
    public static void SetAuthenticated(HttpContext context) =>
        context.Session.SetString(SessionKey, "1");

    /// <summary>Clears the authentication from the current session.</summary>
    public static void ClearAuthenticated(HttpContext context) =>
        context.Session.Remove(SessionKey);
}
