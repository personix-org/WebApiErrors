namespace Personix.WebApiErrors.V1.Http4xx;

/// <summary>
/// Base for HTTP 404 Not Found responses (RFC 9110 §15.5.5).
/// The <c>type</c> field defaults to the RFC section URI.
/// </summary>
public abstract record NotFoundApiError : ApiErrorBase
{
    /// <summary>The RFC 9110 §15.5.5 URI, used unless overridden with a project-specific one.</summary>
    public override string? Type { get; init; } = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5";
}
