using Microsoft.AspNetCore.Http;
using Personix.WebApiErrors.V1.Http4xx;
using Personix.WebApiErrors.V1.Http5xx;

namespace Personix.WebApiErrors.V1;

/// <summary>
/// Maps a problem type to the HTTP status code it represents.
/// </summary>
public static class ApiErrorStatusCodes
{
    /// <summary>
    /// The status code for <paramref name="problem"/>, or <c>null</c> when the type derives
    /// straight from <see cref="ApiErrorBase"/> without a status-specific base.
    /// </summary>
    public static int? For(ApiErrorBase problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        // ValidationApiError needs no branch of its own — it derives from BadRequestApiError and
        // carries the same 400. Should it ever warrant a different code, add it *above* that arm,
        // because the first matching pattern wins.
        return problem switch
        {
            BadRequestApiError => StatusCodes.Status400BadRequest,
            UnauthorizedApiError => StatusCodes.Status401Unauthorized,
            ForbiddenApiError => StatusCodes.Status403Forbidden,
            NotFoundApiError => StatusCodes.Status404NotFound,
            RateLimitApiError => StatusCodes.Status429TooManyRequests,
            InternalServerApiError => StatusCodes.Status500InternalServerError,
            _ => null,
        };
    }
}
