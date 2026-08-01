using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Personix.ApiErrors.Exceptions;
using Personix.WebApiErrors.V1;
using Shouldly;
using Xunit;

namespace Personix.WebApiErrors.Tests;

/// <summary>
/// Covers the one ordering hazard in exception mapping: ValidationException derives from
/// BadRequestException, so a general arm placed first would swallow it and the response would lose
/// the field breakdown it exists to carry.
/// </summary>
public sealed class ExceptionToApiErrorMappingTests
{
    private const string ValidationTitle = "Validation failed";
    private const string ValidationDetail = "The subscription could not be created.";
    private const string BadRequestTitle = "Malformed request";
    private const string BadRequestDetail = "The body was not valid JSON.";
    private const string EmailField = "email";
    private const string EmailMessage = "Must be a valid address.";
    private const string RequestPath = "/api/v1/subscriptions";

    private static ValidationException AValidationException()
        => new(ValidationTitle, ValidationDetail, new Dictionary<string, string[]>
        {
            [EmailField] = [EmailMessage],
        });

    private static (HttpContext Context, MemoryStream Body) RequestTo(string path)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Post;
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
    public void Map_TranslatesValidation_ToTheErrorCarryingTheFieldBreakdown()
    {
        var mapped = new RequestExceptionMapper().Map(AValidationException());

        mapped.ShouldBeOfType<TestValidationApiError>()
            .Errors[EmailField].ShouldBe([EmailMessage]);
    }

    [Fact]
    public void Map_TranslatesAPlainBadRequest_ToTheErrorWithoutAFieldBreakdown()
    {
        var mapped = new RequestExceptionMapper().Map(
            new BadRequestException(BadRequestTitle, BadRequestDetail));

        mapped.ShouldBeOfType<TestBadRequestApiError>();
    }

    [Fact]
    public async Task TryHandleAsync_AnswersValidationWith400AndTheFieldBreakdown()
    {
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new RequestExceptionMapper())
            .TryHandleAsync(context, AValidationException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(body).GetProperty("errors").GetProperty(EmailField)
            .EnumerateArray().Single().GetString().ShouldBe(EmailMessage);
    }

    [Fact]
    public async Task TryHandleAsync_LosesTheFieldBreakdown_WhenAGeneralMapperIsRegisteredFirst()
    {
        // The hazard the compiler cannot catch. Within one switch the wrong order is a build error;
        // split across two mappers it is merely registration order, and the general one wins
        // silently. Keep exceptions that share a hierarchy in a single mapper.
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new BadRequestOnlyMapper(), new ValidationOnlyMapper())
            .TryHandleAsync(context, AValidationException(), CancellationToken.None);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ReadBody(body).TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_KeepsTheFieldBreakdown_WhenTheSpecificMapperComesFirst()
    {
        var (context, body) = RequestTo(RequestPath);

        await HandlerWith(new ValidationOnlyMapper(), new BadRequestOnlyMapper())
            .TryHandleAsync(context, AValidationException(), CancellationToken.None);

        ReadBody(body).TryGetProperty("errors", out _).ShouldBeTrue();
    }

    private sealed class RequestExceptionMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) => exception switch
        {
            // ValidationException is a BadRequestException, so it has to be matched first. Put the
            // arms the other way round and the compiler rejects the file: the second one would be
            // unreachable. That is the whole safeguard — within one switch this cannot go wrong.
            ValidationException e => e.ToApiError(),
            BadRequestException e => e.ToApiError(),
            _ => null,
        };
    }

    private sealed class ValidationOnlyMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) =>
            exception is ValidationException e ? e.ToApiError() : null;
    }

    private sealed class BadRequestOnlyMapper : IExceptionToApiErrorMapper
    {
        public IApiError? Map(Exception exception) =>
            exception is BadRequestException e ? e.ToApiError() : null;
    }
}

// The pair mapping a consuming service writes by hand — one method per exception, so the specifics
// survive the translation instead of being flattened into a generic 400.
internal static class RequestExceptionMappings
{
    public static TestValidationApiError ToApiError(this ValidationException exception) => new()
    {
        Title = exception.Title,
        Detail = exception.Detail,
        Errors = exception.Errors,
    };

    public static TestBadRequestApiError ToApiError(this BadRequestException exception) => new()
    {
        Title = exception.Title,
        Detail = exception.Detail,
    };
}
