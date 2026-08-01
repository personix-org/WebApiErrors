using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Personix.WebApiErrors.V1.Http4xx;

namespace Personix.WebApiErrors.V1;

/// <summary>
/// Turns a problem record into a typed HTTP result, filling in the parts that are otherwise copied
/// by hand into every endpoint — and that drift apart when they are.
/// </summary>
public static class ApiErrorResultExtensions
{
    /// <summary>The media type RFC 9457 clients look for.</summary>
    public const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Returns <paramref name="problem"/> as a typed result carrying the status code implied by its
    /// type, served as <c>application/problem+json</c>.
    /// </summary>
    /// <remarks>
    /// Fills in whatever the caller left unset: <c>status</c> from the problem type, <c>instance</c>
    /// from the request path, and <c>traceId</c> from the ambient activity. Values already set are
    /// kept, so an endpoint can still override any of them.
    /// <para>
    /// The status code goes onto the response and into the body from the same source, so the two
    /// cannot disagree — which is the failure this method exists to prevent.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The problem derives straight from <see cref="ApiErrorBase"/> and no status was set, so there
    /// is nothing to infer the response code from.
    /// </exception>
    public static JsonHttpResult<TProblem> ToApiErrorResult<TProblem>(
        this TProblem problem,
        HttpContext? httpContext = null)
        where TProblem : ApiErrorBase
    {
        ArgumentNullException.ThrowIfNull(problem);

        var status = problem.Status ?? ApiErrorStatusCodes.For(problem);

        if (status is null)
        {
            throw new InvalidOperationException(
                $"Cannot determine a status code for '{typeof(TProblem).Name}'. Derive it from one of "
                + "the status-specific problem types, or set Status explicitly.");
        }

        var enriched = problem with
        {
            Status = status,
            Instance = problem.Instance ?? httpContext?.Request.Path.Value,
            TraceId = problem.TraceId ?? Activity.Current?.TraceId.ToString(),
        };

        ApplyRetryAfter(enriched, httpContext);

        return TypedResults.Json(enriched, statusCode: status, contentType: ProblemJsonContentType);
    }

    /// <summary>
    /// Writes <paramref name="problem"/> to the response, serialising it by its runtime type.
    /// </summary>
    /// <remarks>
    /// For code holding a problem only as <see cref="ApiErrorBase"/> — an exception handler, say.
    /// <see cref="ToApiErrorResult{TProblem}"/> cannot help there: its type argument would bind to
    /// <see cref="ApiErrorBase"/> and <c>System.Text.Json</c> would emit only the base fields,
    /// silently dropping everything the concrete error added.
    /// </remarks>
    public static async Task<int> WriteApiErrorAsync(
        this ApiErrorBase problem,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(httpContext);

        var status = problem.Status ?? ApiErrorStatusCodes.For(problem);

        if (status is null)
        {
            throw new InvalidOperationException(
                $"Cannot determine a status code for '{problem.GetType().Name}'. Derive it from one "
                + "of the status-specific problem types, or set Status explicitly.");
        }

        var enriched = problem with
        {
            Status = status,
            Instance = problem.Instance ?? httpContext.Request.Path.Value,
            TraceId = problem.TraceId ?? Activity.Current?.TraceId.ToString(),
        };

        ApplyRetryAfter(enriched, httpContext);

        httpContext.Response.StatusCode = status.Value;
        httpContext.Response.ContentType = ProblemJsonContentType;

        await httpContext.Response.WriteAsJsonAsync(
            enriched, enriched.GetType(), options: null, contentType: ProblemJsonContentType, cancellationToken);

        return status.Value;
    }

    /// <summary>
    /// Sets <c>Retry-After</c> when the error carries a back-off hint.
    /// </summary>
    /// <remarks>
    /// RFC 6585 §4 expects a 429 to say when the caller may return. Without the header a client
    /// has to guess, and the seconds in the body are only useful to something that parses it.
    /// </remarks>
    private static void ApplyRetryAfter(ApiErrorBase problem, HttpContext? httpContext)
    {
        if (httpContext is null || problem is not RateLimitApiError rateLimit)
        {
            return;
        }

        httpContext.Response.Headers.RetryAfter =
            rateLimit.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
    }
}
