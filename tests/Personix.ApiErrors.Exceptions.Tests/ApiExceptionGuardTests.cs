using Shouldly;
using Xunit;

namespace Personix.ApiErrors.Exceptions.Tests;

/// <summary>
/// The throw site is where the data still exists and where a stack trace is worth something. These
/// tests pin the guards that keep an exception from being raised without it — the same invariants
/// the matching error records enforce, so a pair cannot pass validation on one side and fail on the
/// other.
/// </summary>
public sealed class ApiExceptionGuardTests
{
    private const string AnyTitle = "Subscription not found";
    private const string AnyDetail = "No subscription with id 42 exists.";
    private const string Whitespace = "   ";
    private const string EmailField = "email";
    private const string EmailMessage = "Must be a valid address.";
    private const int RetryAfterSeconds = 30;
    private const int RateLimit = 100;
    private const int WindowSeconds = 60;

    private static IReadOnlyDictionary<string, string[]> AnErrorMap()
        => new Dictionary<string, string[]> { [EmailField] = [EmailMessage] };

    [Fact]
    public void Title_IsRejected_WhenNull()
    {
        Should.Throw<ArgumentNullException>(() => new NotFoundException(null!, AnyDetail));
    }

    [Fact]
    public void Title_IsRejected_WhenBlank()
    {
        Should.Throw<ArgumentException>(() => new NotFoundException(Whitespace, AnyDetail));
    }

    [Fact]
    public void Detail_IsRejected_WhenNull()
    {
        Should.Throw<ArgumentNullException>(() => new NotFoundException(AnyTitle, null!));
    }

    [Fact]
    public void Detail_IsRejected_WhenBlank()
    {
        Should.Throw<ArgumentException>(() => new NotFoundException(AnyTitle, Whitespace));
    }

    [Fact]
    public void Detail_BecomesTheExceptionMessage_SoLogsReadWithoutUnwrapping()
    {
        new NotFoundException(AnyTitle, AnyDetail).Message.ShouldBe(AnyDetail);
    }

    [Fact]
    public void InnerException_IsKept_SoTheOriginalFailureSurvivesTheTranslation()
    {
        var cause = new InvalidOperationException("The underlying failure.");

        new NotFoundException(AnyTitle, AnyDetail, cause).InnerException.ShouldBeSameAs(cause);
    }

    [Theory]
    [InlineData(typeof(BadRequestException))]
    [InlineData(typeof(UnauthorizedException))]
    [InlineData(typeof(ForbiddenException))]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(InternalServerException))]
    public void EveryStatusException_CarriesTitleAndDetail(Type exceptionType)
    {
        var exception = (ApiException)Activator.CreateInstance(exceptionType, AnyTitle, AnyDetail, null)!;

        exception.Title.ShouldBe(AnyTitle);
        exception.Detail.ShouldBe(AnyDetail);
    }

    [Fact]
    public void ValidationException_IsABadRequest_SoOneSwitchArmCanCoverBoth()
    {
        new ValidationException(AnyTitle, AnyDetail, AnErrorMap()).ShouldBeAssignableTo<BadRequestException>();
    }

    [Fact]
    public void ValidationErrors_AreRejected_WhenNull()
    {
        Should.Throw<ArgumentNullException>(() => new ValidationException(AnyTitle, AnyDetail, null!));
    }

    [Fact]
    public void ValidationErrors_AreRejected_WhenEmpty()
    {
        // Matches the error record. Without this the exception is raised happily and the failure
        // surfaces one step later, inside ToApiError, where the throw site is already gone.
        Should.Throw<ArgumentException>(() =>
            new ValidationException(AnyTitle, AnyDetail, new Dictionary<string, string[]>()));
    }

    [Fact]
    public void ValidationErrors_AreKept_AsGiven()
    {
        new ValidationException(AnyTitle, AnyDetail, AnErrorMap())
            .Errors[EmailField].ShouldBe([EmailMessage]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RetryAfterSeconds_IsRejected_WhenNotPositive(int invalid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new RateLimitException(AnyTitle, AnyDetail, invalid, RateLimit, WindowSeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Limit_IsRejected_WhenNotPositive(int invalid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new RateLimitException(AnyTitle, AnyDetail, RetryAfterSeconds, invalid, WindowSeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WindowSeconds_IsRejected_WhenNotPositive(int invalid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new RateLimitException(AnyTitle, AnyDetail, RetryAfterSeconds, RateLimit, invalid));
    }

    [Fact]
    public void RateLimitException_KeepsAllThreeBackOffValues()
    {
        var exception = new RateLimitException(AnyTitle, AnyDetail, RetryAfterSeconds, RateLimit, WindowSeconds);

        exception.RetryAfterSeconds.ShouldBe(RetryAfterSeconds);
        exception.Limit.ShouldBe(RateLimit);
        exception.WindowSeconds.ShouldBe(WindowSeconds);
    }
}
