using Personix.WebApiErrors.V1.Http5xx;

namespace Personix.WebApiErrors.V1;

/// <summary>
/// The response for an exception nothing else claimed. Deliberately says nothing about what went
/// wrong — the detail belongs in the log, reached through the trace id, not in a response that may
/// leave the network.
/// </summary>
public sealed record UnhandledApiError : InternalServerApiError
{
    /// <summary>Default title used when the handler is not configured otherwise.</summary>
    public const string DefaultTitle = "Unexpected error";

    /// <summary>Default detail: tells the caller what to quote when reporting the failure.</summary>
    public const string DefaultDetail =
        "The request could not be completed. Quote the trace id when reporting this.";
}
