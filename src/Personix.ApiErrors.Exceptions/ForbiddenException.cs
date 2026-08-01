namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the caller is authenticated but not allowed to do this — answered with 403 Forbidden.
/// </summary>
public class ForbiddenException : ApiException
{
    /// <inheritdoc />
    public ForbiddenException(string title, string detail, Exception? innerException = null)
        : base(title, detail, innerException)
    {
    }
}
