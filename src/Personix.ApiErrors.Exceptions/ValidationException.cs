namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the request failed field-level validation — answered with 400 Bad Request and a
/// per-field breakdown.
/// </summary>
/// <remarks>
/// Derives from <see cref="BadRequestException"/>, matching the response hierarchy where a
/// validation error is a bad request that happens to know which fields were wrong.
/// </remarks>
public class ValidationException : BadRequestException
{
    /// <param name="title">Short summary of the failure kind.</param>
    /// <param name="detail">What happened this time.</param>
    /// <param name="errors">
    /// Failed fields keyed by property name in camelCase, each with its messages. At least one
    /// entry — the breakdown is the only thing this type adds over a plain
    /// <see cref="BadRequestException"/>.
    /// </param>
    /// <param name="innerException">The failure this one wraps, when there is one.</param>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentException">The map is empty.</exception>
    public ValidationException(
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]> errors,
        Exception? innerException = null)
        : base(title, detail, innerException)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "A validation failure must name at least one field. Throw BadRequestException when "
                + "there is no field-level breakdown to give.", nameof(errors));
        }

        Errors = errors;
    }

    /// <summary>Failed fields keyed by property name, each with its messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
