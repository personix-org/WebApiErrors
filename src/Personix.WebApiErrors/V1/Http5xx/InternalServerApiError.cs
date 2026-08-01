namespace Personix.WebApiErrors.V1.Http5xx;

/// <summary>
/// Base for HTTP 500 Internal Server Error responses (RFC 9110 §15.6.1).
/// The <c>type</c> field defaults to the RFC section URI.
/// Use <c>traceId</c> to locate the full exception in your OTEL backend.
/// </summary>
public abstract record InternalServerApiError : ApiErrorBase
{
    /// <summary>The RFC 9110 §15.6.1 URI, used unless overridden with a project-specific one.</summary>
    public override string? Type { get; init; } = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1";
}
