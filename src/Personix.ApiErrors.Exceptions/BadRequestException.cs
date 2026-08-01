namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the request itself is malformed or contradictory — answered with 400 Bad Request.
/// </summary>
public class BadRequestException : ApiException
{
    /// <inheritdoc />
    public BadRequestException(string title, string detail, Exception? innerException = null)
        : base(title, detail, innerException)
    {
    }
}
