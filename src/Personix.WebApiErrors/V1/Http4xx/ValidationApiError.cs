using System.Text.Json.Serialization;

namespace Personix.WebApiErrors.V1.Http4xx;

/// <summary>
/// Base for HTTP 400 Bad Request responses that carry field-level validation errors
/// (RFC 9110 §15.5.1 + RFC 9457 §4.1 extension).
/// Inherits the RFC 9110 §15.5.1 <c>type</c> URI from <see cref="BadRequestApiError"/>.
/// </summary>
/// <remarks>
/// The <c>errors</c> map follows the ASP.NET Core validation problem format:
/// keys are property names (camelCase), values are arrays of error messages.
/// Example: <c>{ "webhookUrl": ["Must be a valid absolute URL."] }</c>
/// </remarks>
public abstract record ValidationApiError : BadRequestApiError
{
    /// <summary>
    /// Field-level validation errors keyed by property name (camelCase).
    /// </summary>
    /// <remarks>
    /// This map is the only thing distinguishing a validation error from a plain
    /// <see cref="BadRequestApiError"/>, so an empty one is refused: it would produce a 400 that
    /// claims to name the offending fields and then names none.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentException">The map is empty.</exception>
    [JsonPropertyName("errors")]
    public required IReadOnlyDictionary<string, string[]> Errors
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Count == 0)
            {
                throw new ArgumentException(
                    "A validation error must name at least one field. Use BadRequestApiError when "
                    + "there is no field-level breakdown to give.", nameof(value));
            }

            field = value;
        }
    }
}
