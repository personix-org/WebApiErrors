using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Personix.WebApiErrors.V1;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

public sealed class ApiErrorExceptionHandlerTests
{
    private const string AnyDetail = "Something specific about this occurrence.";
    private const string RequestPath = "/api/v1/subscriptions/42";
    private const string MappedTitle = "Subscription not found";
    private const string SecretDetail = "Connection string password was rejected";

    private static (HttpContext Context, MemoryStream Body) RequestTo(string path)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = body;
        return (context, body);
    }

    private static ApiErrorExceptionHandler HandlerWith(params IExceptionToApiErrorMapper[] mappers)
        => new(mappers, NullLogger<ApiErrorExceptionHandler>.Instance);

    private static JsonElement ReadBody(MemoryStream body)
    {
        body.Position = 0;
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task TryHandleAsync_ReportsItHandledTheException()
    {
        var (context, _) = RequestTo(RequestPath);

        var handled = await HandlerWith().TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        handled.ShouldBeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_AnswersUnmappedExceptionsWith500()
    {
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith().TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_ServesProblemJson()
    {
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith().TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        context.Response.ContentType.ShouldStartWith(ApiErrorResultExtensions.ProblemJsonContentType);
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotLeakTheExceptionMessageToTheCaller()
    {
        // The exception text can carry connection strings, paths, or internal identifiers. None of
        // it belongs in a response; the trace id is how the caller points at the log entry instead.
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith().TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        ReadBody(body).GetRawText().ShouldNotContain(SecretDetail);
    }

    [Fact]
    public async Task TryHandleAsync_FillsInstanceFromTheRequestPath()
    {
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith().TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        ReadBody(body).GetProperty("instance").GetString().ShouldBe(RequestPath);
    }

    [Fact]
    public async Task TryHandleAsync_UsesAMapperWhenOneRecognisesTheException()
    {
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(new NotFoundMapper()).TryHandleAsync(
            context, new KeyNotFoundException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_SerialisesTheMappedTypeWithItsOwnFields()
    {
        // Guards the runtime-type serialisation: held as ApiErrorBase, the concrete record's extra
        // fields would silently vanish if the base type drove the serialiser.
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new NotFoundMapper()).TryHandleAsync(
            context, new KeyNotFoundException(), CancellationToken.None);

        var json = ReadBody(body);
        json.GetProperty("title").GetString().ShouldBe(MappedTitle);
        json.GetProperty("resourceKind").GetString().ShouldBe(TestMappedNotFoundApiError.Kind);
    }

    [Fact]
    public async Task TryHandleAsync_FallsBackTo500WhenNoMapperClaimsTheException()
    {
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(new NotFoundMapper()).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_ConsultsMappersInOrderAndTakesTheFirstMatch()
    {
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new NotFoundMapper(), new CatchAllForbiddenMapper()).TryHandleAsync(
            context, new KeyNotFoundException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ReadBody(body).GetProperty("title").GetString().ShouldBe(MappedTitle);
    }

    [Fact]
    public async Task TryHandleAsync_SkipsMappersThatReturnNull()
    {
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(new NotFoundMapper(), new CatchAllForbiddenMapper()).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TryHandleAsync_SurvivesAMapperThatThrows()
    {
        // A mapper is ordinary code and can fail — most plausibly on the guards, by building an
        // error with a blank detail. The one place that must not throw is the handler answering an
        // exception, so a broken mapper degrades to the unmapped answer instead of leaving the
        // caller with a dropped connection.
        var (context, _) = RequestTo(RequestPath);

        var handled = await HandlerWith(new ThrowingMapper()).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_ConsultsTheNextMapper_WhenOneThrows()
    {
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new ThrowingMapper(), new NotFoundMapper()).TryHandleAsync(
            context, new KeyNotFoundException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ReadBody(body).GetProperty("title").GetString().ShouldBe(MappedTitle);
    }

    private sealed class ThrowingMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) =>
            throw new InvalidOperationException("This mapper is broken.");
    }

    private sealed class NotFoundMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) => exception is KeyNotFoundException
            ? new TestMappedNotFoundApiError { Title = MappedTitle, Detail = AnyDetail }
            : null;
    }

    private sealed class CatchAllForbiddenMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) =>
            new TestForbiddenApiError { Title = "Forbidden", Detail = AnyDetail };
    }
}
