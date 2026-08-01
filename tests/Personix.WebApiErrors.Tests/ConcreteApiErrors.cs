using System.Text.Json.Serialization;
using Personix.WebApiErrors.V1;
using Personix.WebApiErrors.V1.Http4xx;
using Personix.WebApiErrors.V1.Http5xx;

namespace Personix.WebApiErrors.Tests;

// Concrete errors of the kind a consuming service declares — one per endpoint per failure mode.
internal sealed record TestUnauthorizedApiError : UnauthorizedApiError;

internal sealed record TestForbiddenApiError : ForbiddenApiError;

internal sealed record TestNotFoundApiError : NotFoundApiError;

internal sealed record TestBadRequestApiError : BadRequestApiError;

internal sealed record TestValidationApiError : ValidationApiError;

internal sealed record TestRateLimitApiError : RateLimitApiError;

internal sealed record TestInternalServerApiError : InternalServerApiError;

// Endpoint-specific context added on top of a status-specific abstraction.
internal sealed record TestNotFoundWithContextApiError : NotFoundApiError
{
    [JsonPropertyName("workerStatus")]
    public string? WorkerStatus { get; init; }
}

// Derives straight from ApiErrorBase, so no status can be inferred from its type.
internal sealed record BareApiError : ApiErrorBase;

// Carries an extra field, so that serialising it through a base-typed reference can be detected.
internal sealed record TestMappedNotFoundApiError : NotFoundApiError
{
    internal const string Kind = "subscription";

    [JsonPropertyName("resourceKind")]
    public string ResourceKind { get; init; } = Kind;
}
