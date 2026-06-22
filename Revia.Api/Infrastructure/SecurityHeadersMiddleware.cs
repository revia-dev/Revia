namespace Revia.Api.Infrastructure;

/// <summary>
/// Adds a baseline set of security response headers to every response. Kept
/// deliberately conservative so it doesn't break the Blazor WASM client (which
/// needs inline/WASM script execution): we set the safe, framework-agnostic
/// headers and leave a strict CSP to a fronting reverse proxy where one is used.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        // Disable powerful features the app never uses.
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), payment=()";
        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
