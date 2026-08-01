namespace Personix.WebApiErrors;

/// <summary>
/// Translates an exception into the problem that should be returned for it.
/// </summary>
/// <remarks>
/// Implement this for domain exceptions that have a meaningful HTTP answer — a missing aggregate
/// becoming a 404, a rejected invariant becoming a 400. Anything left unmapped falls through to
/// <see cref="UnhandledApiError"/>, so a mapper only needs to describe the cases it knows.
/// <para>
/// Register implementations in DI; they are consulted in registration order and the first non-null
/// result wins.
/// </para>
/// </remarks>
public interface IExceptionToApiErrorMapper
{
    /// <summary>
    /// The problem representing <paramref name="exception"/>, or <c>null</c> when this mapper does
    /// not recognise it.
    /// </summary>
    IApiError? Map(Exception exception);
}
