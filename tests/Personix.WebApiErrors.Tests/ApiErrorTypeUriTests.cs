using Personix.WebApiErrors.V1;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

public sealed class ApiErrorTypeUriTests
{
    private const int RetryAfterSeconds = 30;
    private const int RateLimit = 100;
    private const int WindowSeconds = 60;
    private const string AnyDetail = "Something specific about this occurrence.";
    private const string EmailField = "email";
    private const string EmailMessage = "Must be a valid address.";

    [Fact]
    public void UnauthorizedApiError_DefaultsToTheRfc9110AuthenticationSection()
    {
        new TestUnauthorizedApiError { Title = "Unauthorized", Detail = AnyDetail }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2");
    }

    [Fact]
    public void ForbiddenApiError_DefaultsToTheRfc9110ForbiddenSection()
    {
        new TestForbiddenApiError { Title = "Forbidden", Detail = AnyDetail }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4");
    }

    [Fact]
    public void NotFoundApiError_DefaultsToTheRfc9110NotFoundSection()
    {
        new TestNotFoundApiError { Title = "Not found", Detail = AnyDetail }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5");
    }

    [Fact]
    public void BadRequestApiError_DefaultsToTheRfc9110BadRequestSection()
    {
        new TestBadRequestApiError { Title = "Bad request", Detail = AnyDetail }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1");
    }

    [Fact]
    public void RateLimitApiError_DefaultsToTheRfc6585TooManyRequestsSection()
    {
        new TestRateLimitApiError { Title = "Too many requests", Detail = AnyDetail, RetryAfterSeconds = RetryAfterSeconds, Limit = RateLimit, WindowSeconds = WindowSeconds }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc6585#section-4");
    }

    [Fact]
    public void InternalServerApiError_DefaultsToTheRfc9110ServerErrorSection()
    {
        new TestInternalServerApiError { Title = "Server error", Detail = AnyDetail }.Type
            .ShouldBe("https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1");
    }

    [Fact]
    public void ValidationApiError_InheritsTheBadRequestTypeUri()
    {
        var validation = new TestValidationApiError
        {
            Title = "Validation failed",
            Detail = AnyDetail,
            Errors = new Dictionary<string, string[]> { [EmailField] = [EmailMessage] },
        };

        validation.Type.ShouldBe(new TestBadRequestApiError { Title = "Bad request", Detail = AnyDetail }.Type);
    }

    [Fact]
    public void TypeUri_CanBeOverriddenWithAProjectSpecificOne()
    {
        var problem = new TestNotFoundApiError
        {
            Title = "Not found",
            Detail = AnyDetail,
            Type = "https://personix.org/problems/subscription-missing",
        };

        problem.Type.ShouldBe("https://personix.org/problems/subscription-missing");
    }
}
