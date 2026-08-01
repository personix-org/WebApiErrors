using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Personix.WebApiErrors.V1;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

public sealed class ApiErrorResultExtensionsTests
{
    private const int RetryAfterSeconds = 30;
    private const int RateLimit = 100;
    private const int WindowSeconds = 60;
    private const string AnyDetail = "Something specific about this occurrence.";
    private const string RequestPath = "/api/v1/subscriptions/42";
    private const string ExplicitInstance = "/explicit/instance";
    private const string ExplicitTraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string NotFoundTitle = "Subscription not found";
    private const string SourceName = "ApiErrorResultExtensionsTests";
    private const string RateLimitTitle = "Too many requests";
    private const string EmailField = "email";
    private const string EmailMessage = "Must be a valid address.";

    private static HttpContext RequestTo(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    // ExecuteAsync (unlike building the result) needs a response body to write to and an
    // ILoggerFactory in RequestServices — ASP.NET Core's own JsonHttpResult.ExecuteAsync requires
    // one to create its internal logger. Kept separate from RequestTo so the many tests that only
    // build a result, and never execute it, stay exactly as minimal as before.
    private static HttpContext ExecutableRequestTo(string path)
    {
        var context = RequestTo(path);
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();
        return context;
    }

    [Fact]
    public void ToApiErrorResult_InfersTheStatusCodeFromTheApiErrorType()
    {
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToApiErrorResult_PutsTheSameStatusInTheBodyAsOnTheResponse()
    {
        // The failure this method exists to prevent: a 404 response carrying "status": 500.
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.Value!.Status.ShouldBe(result.StatusCode);
    }

    [Fact]
    public void ToApiErrorResult_ServesProblemJson()
    {
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.ContentType.ShouldBe(ApiErrorResultExtensions.ProblemJsonContentType);
    }

    [Fact]
    public async Task ToApiErrorResult_WritesProblemJsonAsTheActualResponseContentTypeHeader()
    {
        // ContentType on the JsonHttpResult is what we control; a caller only ever sees what lands
        // on the response after ASP.NET Core executes the result. For a Problem Details response the
        // header is part of the contract (RFC 9457 clients look for it), not an implementation
        // detail, so it is executed here rather than inferred from the result object alone.
        var context = ExecutableRequestTo(RequestPath);
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        await result.ExecuteAsync(context);

        context.Response.ContentType.ShouldBe(ApiErrorResultExtensions.ProblemJsonContentType);
    }

    [Fact]
    public async Task ToApiErrorResult_WritesTheInferredStatusCodeOntoTheActualResponse()
    {
        var context = ExecutableRequestTo(RequestPath);
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        await result.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status429TooManyRequests)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public void ToApiErrorResult_CoversEveryStatusSpecificBaseType(int expectedStatus)
    {
        var result = ApiErrorFor(expectedStatus).ToApiErrorResult();

        result.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void ToApiErrorResult_TreatsValidationAsBadRequest_DespiteTheDeeperHierarchy()
    {
        var validation = new TestValidationApiError
        {
            Title = "Validation failed",
            Detail = AnyDetail,
            Errors = new Dictionary<string, string[]> { [EmailField] = [EmailMessage] },
        };

        validation.ToApiErrorResult().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToApiErrorResult_KeepsAnExplicitlySetStatus()
    {
        var problem = new TestNotFoundApiError
        {
            Title = NotFoundTitle,
            Detail = AnyDetail,
            Status = StatusCodes.Status410Gone,
        };

        problem.ToApiErrorResult().StatusCode.ShouldBe(StatusCodes.Status410Gone);
    }

    [Fact]
    public void ToApiErrorResult_FillsInstanceFromTheRequestPath()
    {
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }
            .ToApiErrorResult(RequestTo(RequestPath));

        result.Value!.Instance.ShouldBe(RequestPath);
    }

    [Fact]
    public void ToApiErrorResult_KeepsAnExplicitlySetInstance()
    {
        var problem = new TestNotFoundApiError { Title = NotFoundTitle, Instance = ExplicitInstance, Detail = AnyDetail };

        var result = problem.ToApiErrorResult(RequestTo(RequestPath));

        result.Value!.Instance.ShouldBe(ExplicitInstance);
    }

    [Fact]
    public void ToApiErrorResult_LeavesInstanceUnset_WhenThereIsNoHttpContext()
    {
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.Value!.Instance.ShouldBeNull();
    }

    [Fact]
    public void ToApiErrorResult_FillsTraceIdFromTheCurrentActivity()
    {
        using var source = new ActivitySource(SourceName);
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("request");

        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.Value!.TraceId.ShouldBe(activity!.TraceId.ToString());
    }

    [Fact]
    public void ToApiErrorResult_KeepsAnExplicitlySetTraceId()
    {
        var problem = new TestNotFoundApiError { Title = NotFoundTitle, TraceId = ExplicitTraceId, Detail = AnyDetail };

        problem.ToApiErrorResult().Value!.TraceId.ShouldBe(ExplicitTraceId);
    }

    [Fact]
    public void ToApiErrorResult_DoesNotMutateTheOriginalApiError()
    {
        var problem = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail };

        problem.ToApiErrorResult(RequestTo(RequestPath));

        problem.Status.ShouldBeNull();
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public void ToApiErrorResult_ExplainsItself_WhenTheStatusCannotBeInferred()
    {
        var problem = new BareApiError { Title = "Something went wrong", Detail = AnyDetail };

        var exception = Should.Throw<InvalidOperationException>(() => problem.ToApiErrorResult());

        exception.Message.ShouldContain(nameof(BareApiError));
    }

    [Fact]
    public void ToApiErrorResult_ReturnsATypedResult_SoOpenApiCanDescribeIt()
    {
        // The point of TypedResults: the concrete generic argument is what lets ASP.NET Core
        // generate the response schema from the endpoint signature.
        var result = new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult();

        result.ShouldBeOfType<JsonHttpResult<TestNotFoundApiError>>();
    }

    [Fact]
    public void ToApiErrorResult_SetsRetryAfter_SoAClientNeedNotParseTheBody()
    {
        // RFC 6585 §4 expects a 429 to say when the caller may come back. Only the header reaches
        // clients that do not read the body — proxies and generic HTTP stacks among them.
        var context = RequestTo(RequestPath);

        new TestRateLimitApiError { Title = RateLimitTitle, Detail = AnyDetail, RetryAfterSeconds = RetryAfterSeconds, Limit = RateLimit, WindowSeconds = WindowSeconds }
            .ToApiErrorResult(context);

        context.Response.Headers.RetryAfter.ToString().ShouldBe(RetryAfterSeconds.ToString());
    }

    [Fact]
    public void ToApiErrorResult_LeavesRetryAfterUnset_ForErrorsThatAreNotRateLimits()
    {
        var context = RequestTo(RequestPath);

        new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail }.ToApiErrorResult(context);

        context.Response.Headers.RetryAfter.ShouldBeEmpty();
    }

    private static ApiErrorBase ApiErrorFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => new TestBadRequestApiError { Title = "Bad request", Detail = AnyDetail },
        StatusCodes.Status401Unauthorized => new TestUnauthorizedApiError { Title = "Unauthorized", Detail = AnyDetail },
        StatusCodes.Status403Forbidden => new TestForbiddenApiError { Title = "Forbidden", Detail = AnyDetail },
        StatusCodes.Status404NotFound => new TestNotFoundApiError { Title = NotFoundTitle, Detail = AnyDetail },
        StatusCodes.Status429TooManyRequests => new TestRateLimitApiError { Title = RateLimitTitle, Detail = AnyDetail, RetryAfterSeconds = RetryAfterSeconds, Limit = RateLimit, WindowSeconds = WindowSeconds },
        StatusCodes.Status500InternalServerError => new TestInternalServerApiError { Title = "Server error", Detail = AnyDetail },
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "No error type for this status."),
    };
}
