namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Base for failures whose HTTP meaning is already decided at the point they are raised.
/// </summary>
/// <remarks>
/// This package carries no reference to ASP.NET Core, so domain and application code can throw
/// these without the web framework leaking inwards. The web layer maps them to RFC 9457 responses.
/// <para>
/// <see cref="Detail"/> is required on purpose: it carries what happened *this time*, which is the
/// part the caller can act on and the part that would otherwise be lost between the throw site and
/// the response.
/// </para>
/// </remarks>
public abstract class ApiException : Exception
{
    /// <param name="title">Short summary of the failure kind, invariant across occurrences.</param>
    /// <param name="detail">What happened this time. Also becomes the exception message.</param>
    /// <param name="innerException">The failure this one wraps, when there is one.</param>
    protected ApiException(string title, string detail, Exception? innerException = null)
        : base(detail, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        Title = title;
        Detail = detail;
    }

    /// <summary>Short, human-readable summary of the failure kind.</summary>
    public string Title { get; }

    /// <summary>Explanation specific to this occurrence.</summary>
    public string Detail { get; }
}
