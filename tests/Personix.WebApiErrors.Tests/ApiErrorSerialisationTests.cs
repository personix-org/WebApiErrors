using System.Text.Json;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

public sealed class ApiErrorSerialisationTests
{
    private const int RetryAfterSeconds = 30;
    private const int RateLimit = 100;
    private const int WindowSeconds = 60;
    private const string AnyDetail = "Something specific about this occurrence.";

    private const string ServerErrorTitle = "Server error";
    private const string NotFoundTitle = "Subscription not found";
    private const string NotFoundDetail = "No subscription with that id exists.";
    private const string RequestPath = "/subscriptions/42";
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string UnavailableTitle = "Joke unavailable";
    private const string WorkerStatus = "warming-up";
    private const string WebhookUrlField = "webhookUrl";
    private const string EmailField = "email";
    private const string WebhookUrlMessage = "Must be a valid absolute URL.";
    private const string NotFoundOnlyTitle = "Not found";

    private static JsonDocument Serialise<T>(T problem)
        => JsonDocument.Parse(JsonSerializer.Serialize(problem));

    [Fact]
    public void StandardFields_AreEmittedUnderTheirRfcNames()
    {
        var json = Serialise(new TestNotFoundApiError
        {
            Title = NotFoundTitle,
            Status = 404,
            Detail = NotFoundDetail,
            Instance = RequestPath,
            TraceId = TraceId,
        }).RootElement;

        json.GetProperty("type").GetString().ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5");
        json.GetProperty("title").GetString().ShouldBe(NotFoundTitle);
        json.GetProperty("status").GetInt32().ShouldBe(404);
        json.GetProperty("detail").GetString().ShouldBe(NotFoundDetail);
        json.GetProperty("instance").GetString().ShouldBe(RequestPath);
        json.GetProperty("traceId").GetString().ShouldBe(TraceId);
    }

    [Fact]
    public void ValidationErrors_AreEmittedAsAMapOfFieldToMessages()
    {
        var json = Serialise(new TestValidationApiError
        {
            Title = "Validation failed",
            Detail = AnyDetail,
            Status = 400,
            Errors = new Dictionary<string, string[]>
            {
                [WebhookUrlField] = [WebhookUrlMessage],
                [EmailField] = ["Required.", "Must be a valid address."],
            },
        }).RootElement;

        var errors = json.GetProperty("errors");
        errors.GetProperty(WebhookUrlField).EnumerateArray().Single().GetString()
            .ShouldBe(WebhookUrlMessage);
        errors.GetProperty(EmailField).GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void RateLimitMetadata_IsEmittedSoClientsCanBackOffWithoutGuessing()
    {
        var json = Serialise(new TestRateLimitApiError
        {
            Title = "Too many requests",
            Detail = AnyDetail,
            Status = 429,
            RetryAfterSeconds = RetryAfterSeconds,
            Limit = RateLimit,
            WindowSeconds = WindowSeconds,
        }).RootElement;

        json.GetProperty("retryAfterSeconds").GetInt32().ShouldBe(RetryAfterSeconds);
        json.GetProperty("limit").GetInt32().ShouldBe(RateLimit);
        json.GetProperty("windowSeconds").GetInt32().ShouldBe(WindowSeconds);
    }

    [Fact]
    public void EndpointSpecificFields_AreEmittedAlongsideTheStandardOnes()
    {
        var json = Serialise(new TestNotFoundWithContextApiError
        {
            Title = UnavailableTitle,
            Detail = AnyDetail,
            Status = 404,
            WorkerStatus = WorkerStatus,
        }).RootElement;

        json.GetProperty("workerStatus").GetString().ShouldBe(WorkerStatus);
        json.GetProperty("title").GetString().ShouldBe(UnavailableTitle);
    }

    [Fact]
    public void OptionalFieldsLeftUnset_AreStillEmitted()
    {
        // Everything the service knows goes on the wire, nulls included: a caller reading a fixed
        // shape can tell "not applicable" from "field I do not know about" without guessing.
        var json = Serialise(new TestInternalServerApiError
        {
            Title = ServerErrorTitle,
            Detail = AnyDetail,
        }).RootElement;

        json.GetProperty("instance").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("traceId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void TitleAndDetail_AreRequiredByTheCompiler_SoNoErrorGoesOutEmpty()
    {
        var json = Serialise(new TestInternalServerApiError
        {
            Title = ServerErrorTitle,
            Detail = AnyDetail,
        }).RootElement;

        json.GetProperty("title").GetString().ShouldBe(ServerErrorTitle);
        json.GetProperty("detail").GetString().ShouldBe(AnyDetail);
        json.GetProperty("type").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Status_IsNotInferredFromTheApiErrorType()
    {
        // Documents current behaviour: each concrete error must set its own status.
        var problem = new TestUnauthorizedApiError { Title = "Unauthorized", Detail = AnyDetail };

        problem.Status.ShouldBeNull();
    }

    [Fact]
    public void ApiErrorsAreValueTypes_SoTwoErrorsWithTheSameContentAreEqual()
    {
        var first = new TestNotFoundApiError { Title = NotFoundOnlyTitle, Status = 404, Detail = AnyDetail };
        var second = new TestNotFoundApiError { Title = NotFoundOnlyTitle, Status = 404, Detail = AnyDetail };

        first.ShouldBe(second);
    }
}
