using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Revia.Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into a consistent RFC 7807 ProblemDetails response
/// (instead of leaking a stack trace) and logs it with the request's trace id, so a
/// 500 can be correlated to a log line. Registered via <c>AddExceptionHandler</c> +
/// <c>UseExceptionHandler</c>; returns <c>true</c> once it has written the response.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails = problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path} (trace {TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                // Never expose exception details to the client; the trace id lets an
                // operator find the full error in the logs.
                Detail = $"Trace id: {traceId}",
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1"
            }
        });
    }
}
