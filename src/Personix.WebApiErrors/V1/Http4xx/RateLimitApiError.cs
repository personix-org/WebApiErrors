using System.Text.Json.Serialization;

namespace Personix.WebApiErrors.V1.Http4xx;

/// <summary>
/// Base for HTTP 429 Too Many Requests responses (RFC 6585 §4).
/// </summary>
/// <remarks>
/// The back-off metadata is required rather than optional. A 429 that says only "too many" leaves
/// the caller to guess when to return, and guessing wrong is what turns a rate limit into an
/// outage. All three values are known wherever the limit is enforced, so there is no reason to
/// omit them.
/// </remarks>
public abstract record RateLimitApiError : ApiErrorBase
{
    /// <summary>The RFC 6585 §4 URI, used unless overridden with a project-specific one.</summary>
    public override string? Type { get; init; } = "https://www.rfc-editor.org/rfc/rfc6585#section-4";

    /// <summary>
    /// How many seconds the caller has to wait before another request will be accepted. Sent as the
    /// <c>Retry-After</c> header too, so that clients which never read the body still back off.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative — there is no such thing as waiting for no time at all, and a
    /// zero would invite the caller to retry immediately into the same limit.
    /// </exception>
    [JsonPropertyName("retryAfterSeconds")]
    public required int RetryAfterSeconds
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    }

    /// <summary>
    /// How many requests the caller may make before being rejected. Counted over
    /// <see cref="WindowSeconds"/>, so the two read together as "at most this many, per that long".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative. A limit of none is not a rate limit — it is a closed door, and
    /// that is a 403.
    /// </exception>
    [JsonPropertyName("limit")]
    public required int Limit
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    }

    /// <summary>
    /// The period the <see cref="Limit"/> is counted over. A limit of 100 with a window of 60 means
    /// a hundred requests a minute — the window is what turns a bare number into a rate.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative. Without a window the limit is a bare number the caller cannot
    /// interpret.
    /// </exception>
    [JsonPropertyName("windowSeconds")]
    public required int WindowSeconds
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    }
}
