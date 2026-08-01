using System.Text.Json.Serialization;
using Personix.WebApiErrors.V1.Http4xx;
using Personix.WebApiErrors.V1.Http5xx;

namespace Personix.WebApiErrors.V1;

/// <summary>
/// Abstract base for all RFC 9457 Problem Details responses.
/// Provides the standard fields defined in RFC 9457 §3 plus a <c>traceId</c>
/// extension for W3C Trace Context correlation.
/// </summary>
/// <remarks>
/// Derive from one of the status-specific abstractions rather than from this type directly:
/// <see cref="UnauthorizedApiError"/>, <see cref="ForbiddenApiError"/>,
/// <see cref="NotFoundApiError"/>, <see cref="BadRequestApiError"/>,
/// <see cref="ValidationApiError"/>, <see cref="RateLimitApiError"/>,
/// <see cref="InternalServerApiError"/>.
/// </remarks>
public abstract record ApiErrorBase : IApiError
{
    /// <summary>
    /// A URI reference identifying the problem type.
    /// Each status-specific subclass sets an RFC-correct default;
    /// override with a project-specific URI for custom problem types.
    /// </summary>
    /// <remarks>
    /// RFC 9457 §3.1.1: a consumer that receives no <c>type</c> treats it as <c>about:blank</c>.
    /// </remarks>
    [JsonPropertyName("type")]
    public virtual string? Type { get; init; }

    /// <summary>Short, human-readable summary of the problem type (invariant across occurrences).</summary>
    /// <remarks>
    /// RFC 9457 leaves this optional; it is required here on purpose, because an error response
    /// without a summary tells the caller nothing.
    /// <para>
    /// <c>required</c> forces the initialiser to be written, not to hold anything — <c>null!</c>, a
    /// nullable variable, or a deserialised <c>null</c> all get past it. The guard closes that gap
    /// at the moment the error is built, where the stack trace still points at the culprit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The value is null, empty, or only whitespace.</exception>
    [JsonPropertyName("title")]
    public required string Title
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// The HTTP status code sent with this response. Filled in from the error type when the result
    /// is built, so that the body and the response line cannot claim different things.
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    /// <summary>Human-readable explanation specific to this occurrence.</summary>
    /// <remarks>
    /// RFC 9457 leaves this optional; it is required here so that the compiler forces every error
    /// to say what actually happened this time, rather than repeating the invariant
    /// <see cref="Title"/>. An error a caller cannot act on is a support ticket.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is null, empty, or only whitespace.</exception>
    [JsonPropertyName("detail")]
    public required string Detail
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// Which request this went wrong on — normally its path. Distinguishes one occurrence from
    /// another when the same error appears in several places.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>
    /// The trace this request belongs to. A caller who quotes it in a support request lets you find
    /// the exact run in the observability backend — including the exception the response withheld.
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}
