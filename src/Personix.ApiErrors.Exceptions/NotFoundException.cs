namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the requested resource does not exist — answered with 404 Not Found.
/// </summary>
public class NotFoundException : ApiException
{
    /// <inheritdoc />
    public NotFoundException(string title, string detail, Exception? innerException = null)
        : base(title, detail, innerException)
    {
    }
}
