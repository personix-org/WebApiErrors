using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Personix.WebApiErrors.V1;

namespace Personix.WebApiErrors;

/// <summary>
/// Answers unhandled exceptions with an RFC 9457 problem instead of a stack trace or an HTML error
/// page.
/// </summary>
/// <remarks>
/// Registered mappers are consulted first, so domain exceptions can produce their own status codes.
/// Anything left over becomes <see cref="UnhandledApiError"/> — logged in full, but described to
/// the caller only by its trace id.
/// </remarks>
public sealed class ApiErrorExceptionHandler(
    IEnumerable<IExceptionToApiErrorMapper> mappers,
    ILogger<ApiErrorExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var apiError = MapToApiError(exception);

        // Mapped exceptions are expected outcomes and belong at warning; anything unmapped got
        // past every layer that should have handled it and is a genuine error.
        if (apiError is UnhandledApiError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Request failed for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        await apiError.WriteApiErrorAsync(httpContext, cancellationToken);
        return true;
    }

    private ApiErrorBase MapToApiError(Exception exception)
    {
        foreach (var mapper in mappers)
        {
            ApiErrorBase? mapped;

            try
            {
                mapped = mapper.Map(exception) as ApiErrorBase;
            }
            catch (Exception failure)
            {
                // This is the one place in the application that must answer no matter what. A mapper
                // that throws — most plausibly on an error record's guards — would otherwise take
                // the whole response with it and leave the caller with a dropped connection. Log it
                // as the bug it is and let the next mapper try.
                logger.LogError(failure, "Mapper {Mapper} threw while translating {Exception}",
                    mapper.GetType().Name, exception.GetType().Name);
                continue;
            }

            if (mapped is not null)
            {
                return mapped;
            }
        }

        return new UnhandledApiError
        {
            Title = UnhandledApiError.DefaultTitle,
            Detail = UnhandledApiError.DefaultDetail,
        };
    }
}
