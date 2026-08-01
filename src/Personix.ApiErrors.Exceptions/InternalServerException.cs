namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the request failed for a reason the caller cannot fix — answered with 500 Internal Server Error.
/// </summary>
public class InternalServerException : ApiException
{
    /// <inheritdoc />
    public InternalServerException(string title, string detail, Exception? innerException = null)
        : base(title, detail, innerException)
    {
    }
}
