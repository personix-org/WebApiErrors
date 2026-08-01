namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the caller is not authenticated — answered with 401 Unauthorized.
/// </summary>
public class UnauthorizedException : ApiException
{
    /// <inheritdoc />
    public UnauthorizedException(string title, string detail, Exception? innerException = null)
        : base(title, detail, innerException)
    {
    }
}
