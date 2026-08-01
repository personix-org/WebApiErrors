using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

/// <summary>
/// Covers the entry point every consumer actually goes through. Every other test in this suite
/// builds <see cref="ApiErrorExceptionHandler"/> by hand with <c>new</c>, which proves the handler's
/// own logic works but says nothing about <see cref="ApiErrorHandlingServiceRegistration"/> — the two
/// extension methods a consuming service calls instead. A registration bug (wrong service type, a
/// lifetime that silently changed, a mapper that never reaches the container) would pass every one of
/// those tests and still break every real caller.
/// </summary>
public sealed class ApiErrorHandlingServiceRegistrationTests
{
    private const string MappedTitle = "Subscription not found";
    private const string MappedDetail = "No subscription with that id exists.";
    private const string RequestPath = "/api/v1/subscriptions/42";

    private static ServiceCollection ServicesWithNullLogging()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    private static (HttpContext Context, MemoryStream Body) RequestTo(string path)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = body;
        return (context, body);
    }

    [Fact]
    public void AddApiErrorExceptionHandling_RequiresTheServiceCollection()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddApiErrorExceptionHandling());
    }

    [Fact]
    public void AddApiErrorExceptionHandling_ReturnsTheSameCollection_ForFluentChaining()
    {
        var services = new ServiceCollection();

        services.AddApiErrorExceptionHandling().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddApiErrorExceptionHandling_RegistersTheHandlerSoUseExceptionHandlerCanFindIt()
    {
        var services = ServicesWithNullLogging();
        services.AddApiErrorExceptionHandling();

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IEnumerable<IExceptionHandler>>();

        handlers.ShouldHaveSingleItem().ShouldBeOfType<ApiErrorExceptionHandler>();
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_RequiresTheServiceCollection()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddExceptionToApiErrorMapper<FirstMapper>());
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_ReturnsTheSameCollection_ForFluentChaining()
    {
        var services = new ServiceCollection();

        services.AddExceptionToApiErrorMapper<FirstMapper>().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_RegistersTheMapperForResolution()
    {
        var services = new ServiceCollection();
        services.AddExceptionToApiErrorMapper<FirstMapper>();

        using var provider = services.BuildServiceProvider();
        var mappers = provider.GetRequiredService<IEnumerable<IExceptionToApiErrorMapper>>();

        mappers.ShouldHaveSingleItem().ShouldBeOfType<FirstMapper>();
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_PreservesRegistrationOrder()
    {
        // Mappers are consulted in registration order and the first non-null result wins (README),
        // so the container has to hand them back in that same order — reversed or grouped-by-type
        // would silently change which mapper answers an exception both recognise.
        var services = new ServiceCollection();
        services.AddExceptionToApiErrorMapper<FirstMapper>();
        services.AddExceptionToApiErrorMapper<SecondMapper>();

        using var provider = services.BuildServiceProvider();
        var mapperTypes = provider.GetRequiredService<IEnumerable<IExceptionToApiErrorMapper>>()
            .Select(mapper => mapper.GetType());

        mapperTypes.ShouldBe([typeof(FirstMapper), typeof(SecondMapper)]);
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_DoesNotDuplicate_WhenTheSameMapperTypeIsRegisteredTwice()
    {
        // TryAddEnumerable rather than AddSingleton is the deliberate choice being guarded here: two
        // consumers (or a shared extension method called twice) registering the same mapper type
        // must not make it run — and log — twice per exception.
        var services = new ServiceCollection();
        services.AddExceptionToApiErrorMapper<FirstMapper>();
        services.AddExceptionToApiErrorMapper<FirstMapper>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEnumerable<IExceptionToApiErrorMapper>>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddExceptionToApiErrorMapper_RegistersTheMapperAsASingleton_SoAllScopesShareOneInstance()
    {
        // The trap: resolving twice from the root provider passes even if the registration were
        // Scoped, because the root provider is itself a scope and caches a scoped service exactly
        // like a singleton. Two *separate, explicit* scopes are what actually tells the lifetimes
        // apart — a Scoped registration would hand back a different instance per scope here.
        var services = new ServiceCollection();
        services.AddExceptionToApiErrorMapper<FirstMapper>();
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();
        var fromScopeA = scopeA.ServiceProvider.GetRequiredService<IEnumerable<IExceptionToApiErrorMapper>>().Single();
        var fromScopeB = scopeB.ServiceProvider.GetRequiredService<IEnumerable<IExceptionToApiErrorMapper>>().Single();

        fromScopeA.ShouldBeSameAs(fromScopeB);
    }

    [Fact]
    public async Task ApiErrorExceptionHandling_WorksEndToEnd_WhenWiredEntirelyThroughDI()
    {
        // Nothing here is constructed with `new` — this is exactly the object graph a consumer
        // following the README ends up with: AddApiErrorExceptionHandling + one mapper registration,
        // then a real exception through the resolved handler.
        var services = ServicesWithNullLogging();
        services.AddApiErrorExceptionHandling();
        services.AddExceptionToApiErrorMapper<FirstMapper>();
        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IEnumerable<IExceptionHandler>>()
            .OfType<ApiErrorExceptionHandler>().Single();
        var (context, _) = RequestTo(RequestPath);

        var handled = await handler.TryHandleAsync(context, new KeyNotFoundException(), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    private sealed class FirstMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) => exception is KeyNotFoundException
            ? new TestNotFoundApiError { Title = MappedTitle, Detail = MappedDetail }
            : null;
    }

    private sealed class SecondMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) => null;
    }
}
