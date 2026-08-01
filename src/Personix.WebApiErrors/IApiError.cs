namespace Personix.WebApiErrors;

/// <summary>
/// The version-independent surface of an API error: what the error-handling infrastructure needs
/// to know about any error, regardless of which version defined it.
/// </summary>
/// <remarks>
/// The handler and mappers live outside the versioned folders and talk to this interface, so a
/// future V2 hierarchy is served by the same pipeline without touching it.
/// </remarks>
public interface IApiError
{
    /// <summary>Short, human-readable summary of the failure kind.</summary>
    string Title { get; }

    /// <summary>Explanation specific to this occurrence.</summary>
    string Detail { get; }

    /// <summary>HTTP status code, when the error carries one.</summary>
    int? Status { get; }
}
