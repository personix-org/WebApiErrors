using System.Text.Json;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

/// <summary>
/// Covers what <c>required</c> alone does not: it forces the initialiser to be written, not to
/// contain anything. These guards move the failure to the moment the error is built — a stack trace
/// pointing at the offending line — instead of letting a half-empty response reach the caller.
/// </summary>
public sealed class ApiErrorInvariantTests
{
    private const string AnyTitle = "Subscription not found";
    private const string AnyDetail = "No subscription with that id exists.";
    private const string Whitespace = "   ";
    private const string EmailField = "email";
    private const string EmailMessage = "Must be a valid address.";
    private const int RetryAfterSeconds = 30;
    private const int RateLimit = 100;
    private const int WindowSeconds = 60;

    [Fact]
    public void Title_RejectsNull_WhereTheCompilerOnlyWarns()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TestNotFoundApiError { Title = null!, Detail = AnyDetail });
    }

    [Fact]
    public void Title_RejectsWhitespace_BecauseABlankSummaryIsAsUselessAsNone()
    {
        Should.Throw<ArgumentException>(() =>
            new TestNotFoundApiError { Title = Whitespace, Detail = AnyDetail });
    }

    [Fact]
    public void Detail_RejectsNull_WhereTheCompilerOnlyWarns()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TestNotFoundApiError { Title = AnyTitle, Detail = null! });
    }

    [Fact]
    public void Detail_RejectsWhitespace_BecauseABlankExplanationIsAsUselessAsNone()
    {
        Should.Throw<ArgumentException>(() =>
            new TestNotFoundApiError { Title = AnyTitle, Detail = Whitespace });
    }

    [Fact]
    public void ValidationErrors_RejectNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TestValidationApiError { Title = AnyTitle, Detail = AnyDetail, Errors = null! });
    }

    [Fact]
    public void ValidationErrors_RejectAnEmptyMap_BecauseAValidationErrorWithoutFieldsSaysNothing()
    {
        Should.Throw<ArgumentException>(() => new TestValidationApiError
        {
            Title = AnyTitle,
            Detail = AnyDetail,
            Errors = new Dictionary<string, string[]>(),
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RetryAfterSeconds_RejectsAnythingACallerCannotWaitFor(int invalid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TestRateLimitApiError
        {
            Title = AnyTitle,
            Detail = AnyDetail,
            RetryAfterSeconds = invalid,
            Limit = RateLimit,
            WindowSeconds = WindowSeconds,
        });
    }

    [Fact]
    public void Limit_RejectsZero_BecauseARateOfNoneIsNotARateLimit()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TestRateLimitApiError
        {
            Title = AnyTitle,
            Detail = AnyDetail,
            RetryAfterSeconds = RetryAfterSeconds,
            Limit = 0,
            WindowSeconds = WindowSeconds,
        });
    }

    [Fact]
    public void WindowSeconds_RejectsZero_BecauseALimitWithoutAWindowIsNotARate()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TestRateLimitApiError
        {
            Title = AnyTitle,
            Detail = AnyDetail,
            RetryAfterSeconds = RetryAfterSeconds,
            Limit = RateLimit,
            WindowSeconds = 0,
        });
    }

    [Fact]
    public void Guards_RejectDeserialisedNulls_WhichRequiredAloneLetsThrough()
    {
        // `required` only checks that the JSON carried the property, not that it carried a value.
        const string json = """{"title":null,"detail":"Something went wrong."}""";

        Should.Throw<Exception>(() => JsonSerializer.Deserialize<TestNotFoundApiError>(json));
    }

    [Fact]
    public void Guards_LeaveValidErrorsAlone()
    {
        var error = new TestNotFoundApiError { Title = AnyTitle, Detail = AnyDetail };

        error.Title.ShouldBe(AnyTitle);
        error.Detail.ShouldBe(AnyDetail);
    }

    [Fact]
    public void Guards_DoNotBreakWithExpressions()
    {
        // `with` copies the backing fields directly rather than replaying the init setters, so
        // enriching an error must not re-run — or trip over — the guards.
        var error = new TestValidationApiError
        {
            Title = AnyTitle,
            Detail = AnyDetail,
            Errors = new Dictionary<string, string[]> { [EmailField] = [EmailMessage] },
        };

        var enriched = error with { Status = 400 };

        enriched.Title.ShouldBe(AnyTitle);
        enriched.Errors[EmailField].ShouldBe([EmailMessage]);
    }
}
