using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

/// <summary>
/// Covers what the handler writes to the log, not just what it writes to the response. Every
/// existing handler test builds it with <c>NullLogger</c>, which discards whatever it is given —
/// so nothing so far has checked the log level, that a message is emitted at all, or that the full
/// exception (as opposed to the client-safe response) reaches it.
/// </summary>
public sealed class ApiErrorExceptionHandlerLoggingTests
{
    private const string RequestPath = "/api/v1/subscriptions/42";
    private const string SecretDetail = "Connection string password was rejected";
    private const string MapperFailureMessage = "This mapper is broken.";
    private const string MappedTitle = "Subscription not found";
    private const string MappedDetail = "No subscription with that id exists.";

    private static (HttpContext Context, MemoryStream Body) RequestTo(string path)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = body;
        return (context, body);
    }

    private static ApiErrorExceptionHandler HandlerWith(
        FakeLogger<ApiErrorExceptionHandler> logger, params IExceptionToApiErrorMapper[] mappers)
        => new(mappers, logger);

    [Fact]
    public async Task TryHandleAsync_LogsAtError_WhenNoMapperClaimsTheException()
    {
        // Nothing recognised it, so it got past every layer that should have handled it — that is
        // what makes it error rather than warning.
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(logger).TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        logger.LatestRecord.Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task TryHandleAsync_LogsAtWarning_WhenAMapperClaimsTheException()
    {
        // A mapped exception is an expected outcome the caller's own code raised on purpose, so it
        // does not deserve the same severity as something nothing anticipated.
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(logger, new NotFoundMapper()).TryHandleAsync(
            context, new KeyNotFoundException(), CancellationToken.None);

        logger.LatestRecord.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task TryHandleAsync_LogsExactlyOnce_PerHandledException()
    {
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(logger).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        logger.Collector.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TryHandleAsync_AttachesTheExceptionObject_SoTheStackTraceReachesTheLog()
    {
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);
        var exception = new InvalidOperationException(SecretDetail);

        await HandlerWith(logger).TryHandleAsync(context, exception, CancellationToken.None);

        logger.LatestRecord.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task TryHandleAsync_LogsTheFullDetail_ThatTheResponseWithholdsFromTheCaller()
    {
        // The two promises together, in one test: the log keeps everything, the response keeps
        // nothing beyond a trace id. Either one failing alone is a bug — a stripped log is
        // undebuggable, a full response is a leak — so both are asserted from the same run.
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(logger).TryHandleAsync(
            context, new InvalidOperationException(SecretDetail), CancellationToken.None);

        logger.LatestRecord.Exception!.Message.ShouldBe(SecretDetail);
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).ShouldNotContain(SecretDetail);
    }

    [Fact]
    public async Task TryHandleAsync_NamesTheRequestMethodAndPath_InTheLogState()
    {
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(logger).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        logger.LatestRecord.GetStructuredStateValue("Method").ShouldBe(HttpMethods.Get);
        logger.LatestRecord.GetStructuredStateValue("Path").ShouldBe(RequestPath);
    }

    [Fact]
    public async Task TryHandleAsync_LogsTheMapperFailure_SeparatelyFromTheRequestOutcome()
    {
        // The handler survives a broken mapper (covered elsewhere) by logging the failure and moving
        // on — but that log call itself had no test before this one. Two records are expected: the
        // mapper's own failure, then the eventual unmapped-exception outcome for the request.
        var logger = new FakeLogger<ApiErrorExceptionHandler>();
        var (context, _) = RequestTo(RequestPath);

        await HandlerWith(logger, new ThrowingMapper()).TryHandleAsync(
            context, new InvalidOperationException(), CancellationToken.None);

        var records = logger.Collector.GetSnapshot();
        records.Count.ShouldBe(2);
        records[0].Level.ShouldBe(LogLevel.Error);
        records[0].Exception!.Message.ShouldBe(MapperFailureMessage);
        records[0].GetStructuredStateValue("Mapper").ShouldBe(nameof(ThrowingMapper));
        records[0].GetStructuredStateValue("Exception").ShouldBe(nameof(InvalidOperationException));
        records[1].Level.ShouldBe(LogLevel.Error);
    }

    private sealed class ThrowingMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) =>
            throw new InvalidOperationException(MapperFailureMessage);
    }

    private sealed class NotFoundMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) => exception is KeyNotFoundException
            ? new TestNotFoundApiError { Title = MappedTitle, Detail = MappedDetail }
            : null;
    }
}
