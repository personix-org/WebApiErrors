namespace Personix.ApiErrors.Exceptions;

/// <summary>
/// Signals that the caller has exceeded the permitted rate — answered with 429 Too Many Requests.
/// </summary>
/// <remarks>
/// The back-off data is required, not optional. A 429 that does not say when to come back leaves
/// the caller guessing, and guessing wrong is what turns a rate limit into an outage. All of it is
/// known at the throw site, so it is demanded there rather than hoped for later.
/// </remarks>
public class RateLimitException : ApiException
{
    /// <param name="title">Short summary of the failure kind.</param>
    /// <param name="detail">What happened this time.</param>
    /// <param name="retryAfterSeconds">
    /// How long the caller must wait before trying again. Reaches the client both in the body and
    /// as the <c>Retry-After</c> header.
    /// </param>
    /// <param name="limit">How many requests are allowed before the caller is turned away.</param>
    /// <param name="windowSeconds">
    /// The period the limit is counted over. Together with <paramref name="limit"/> this expresses
    /// the actual rate — 100 and 60 meaning a hundred requests a minute.
    /// </param>
    /// <param name="innerException">The failure this one wraps, when there is one.</param>
    public RateLimitException(
        string title,
        string detail,
        int retryAfterSeconds,
        int limit,
        int windowSeconds,
        Exception? innerException = null)
        : base(title, detail, innerException)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retryAfterSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

        RetryAfterSeconds = retryAfterSeconds;
        Limit = limit;
        WindowSeconds = windowSeconds;
    }

    /// <summary>How many seconds the caller has to wait before another request is accepted.</summary>
    public int RetryAfterSeconds { get; }

    /// <summary>How many requests are allowed before the caller is turned away.</summary>
    public int Limit { get; }

    /// <summary>The period the limit is counted over, in seconds.</summary>
    public int WindowSeconds { get; }
}
