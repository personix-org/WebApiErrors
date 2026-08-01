namespace Personix.WebApiErrors.V1.Http4xx;

/// <summary>
/// Base for HTTP 400 Bad Request responses (RFC 9110 §15.5.1).
/// The <c>type</c> field defaults to the RFC section URI.
/// For field-level validation errors prefer <see cref="ValidationApiError"/>.
/// </summary>
public abstract record BadRequestApiError : ApiErrorBase
{
    /// <summary>The RFC 9110 §15.5.1 URI, used unless overridden with a project-specific one.</summary>
    public override string? Type { get; init; } = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1";
}
