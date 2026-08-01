using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Personix.WebApiErrors.V1;

namespace Personix.WebApiErrors;

/// <summary>
/// Registration for the exception handler and its mappers.
/// </summary>
public static class ApiErrorHandlingServiceRegistration
{
    /// <summary>
    /// Registers <see cref="ApiErrorExceptionHandler"/> so that unhandled exceptions are answered
    /// with an RFC 9457 problem.
    /// </summary>
    /// <remarks>
    /// Call <c>app.UseExceptionHandler()</c> in the pipeline as well — registration alone does not
    /// put the handler in the request path.
    /// </remarks>
    public static IServiceCollection AddApiErrorExceptionHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<ApiErrorExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Registers a mapper translating domain exceptions into problems.
    /// </summary>
    /// <remarks>
    /// Mappers are consulted in registration order and the first non-null result wins, so register
    /// specific ones before general ones.
    /// </remarks>
    public static IServiceCollection AddExceptionToApiErrorMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IExceptionToApiErrorMapper
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IExceptionToApiErrorMapper, TMapper>());
        return services;
    }
}
