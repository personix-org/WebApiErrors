# Personix.WebApiErrors

RFC 9457 Problem Details base types for HTTP APIs. Every error response in a service ends up with the
same shape — machine-readable `type`, human-readable `title` and `detail`, and a `traceId` that leads
straight to the trace in your observability backend — without repeating the boilerplate per endpoint.

## Hierarchy

Three layers. The package supplies the first two; you write the third.

```
ApiErrorBase (abstract)          — RFC 9457 §3 fields + traceId extension
├── UnauthorizedApiError         — 401, RFC 9110 §15.5.2
├── ForbiddenApiError            — 403, RFC 9110 §15.5.4
├── NotFoundApiError             — 404, RFC 9110 §15.5.5
├── BadRequestApiError           — 400, RFC 9110 §15.5.1
│   └── ValidationApiError       — 400 + errors map (RFC 9457 §4.1)
├── RateLimitApiError            — 429, RFC 6585 §4 + retryAfterSeconds / limit / windowSeconds
└── InternalServerApiError          — 500, RFC 9110 §15.6.1
```

## Installation

```xml
<PackageReference Include="Personix.WebApiErrors" Version="1.0.0" />
```

## Usage

### 1. Declare one error per endpoint per failure mode

The status-specific base already carries the correct `type` URI, so most errors are a single line:

```csharp
using Personix.WebApiErrors.V1;

public sealed record GetLatestJokeUnauthenticatedError : UnauthorizedApiError;
public sealed record GetLatestJokeServerError          : InternalServerApiError;
public sealed record GetLatestJokeRateLimitError       : RateLimitApiError;
public sealed record CreateSubscriptionValidationError : ValidationApiError;
```

Naming them per endpoint rather than sharing one `NotFoundError` across the service is deliberate —
it lets each endpoint document its own failures and add its own context.

### 2. Add endpoint-specific context when it helps the caller

```csharp
public sealed record GetLatestJokeUnavailableError : NotFoundApiError
{
    [JsonPropertyName("workerStatus")]
    public string? WorkerStatus { get; init; }
}
```

### 3. Return it

`ToApiErrorResult()` turns the record into a typed result. It fills in what you left unset — the
status code from the problem type, `instance` from the request path, `traceId` from the ambient
activity — and serves it as `application/problem+json`.

```csharp
app.MapGet("/jokes/latest", Results<Ok<JokeDto>, JsonHttpResult<GetLatestJokeUnavailableError>>
    (HttpContext http) =>
{
    if (joke is null)
    {
        return new GetLatestJokeUnavailableError
        {
            Title = "No joke available",
            Detail = "The joke worker has not produced anything yet.",
            WorkerStatus = "warming-up",
        }.ToApiErrorResult(http);
    }

    return TypedResults.Ok(joke.ToDto());
});
```

Two things follow from writing it this way.

**The status code cannot disagree with the body.** Both come from the same place, so a 404 response
can no longer carry `"status": 500` because someone edited one and not the other.

**OpenAPI is generated from the signature.** Declaring the return type as
`Results<Ok<JokeDto>, JsonHttpResult<GetLatestJokeUnavailableError>>` tells ASP.NET Core that this
endpoint answers 200 with a `JokeDto` and 404 with that specific error — including the full schema
of both. No `.Produces<T>(404)` to keep in sync by hand.

That is also why these types exist rather than the framework's `ProblemDetails`: its extension data
is an `IDictionary<string, object?>`, which OpenAPI can only describe as an open-ended object. A
sealed record describes itself.

Passing the `HttpContext` is optional — without it `instance` simply stays unset.

### 4. Validation errors

The `errors` map follows the ASP.NET Core convention: keys are camelCase property names, values are
arrays of messages.

```csharp
return new CreateSubscriptionValidationError
{
    Title = "Validation failed",
    Detail = "Two fields need correcting before the subscription can be created.",
    Errors = new Dictionary<string, string[]>
    {
        ["webhookUrl"] = ["Must be a valid absolute URL."],
        ["email"] = ["Required."],
    },
}.ToApiErrorResult(http);
```

### 5. Unhandled exceptions

The helpers above cover failures an endpoint expects. Everything else needs a net underneath, or a
stack trace or HTML error page escapes to the caller. The package ships one:

```csharp
builder.Services.AddApiErrorExceptionHandling();

var app = builder.Build();
app.UseExceptionHandler();
```

Anything unhandled now comes back as `UnhandledApiError` — a 500 that says nothing beyond a trace
id, while the exception itself is logged in full. The detail belongs in the log, not in a response
that may leave the network.

### 6. Domain exceptions with a meaningful answer

An exception and the error it becomes are a **pair**, and both carry the same specifics. The
package supplies only the bases — the pair itself is yours, because only you know what the caller
needs to know.

Install `Personix.ApiErrors.Exceptions` in the layer that throws; it has no reference to ASP.NET
Core, so domain and application code stay free of the web framework.

**The exception carries the data:**

```csharp
using Personix.ApiErrors.Exceptions;

public sealed class SubscriptionNotFoundException(Guid subscriptionId)
    : NotFoundException(
        title: "Subscription not found",
        detail: $"No subscription with id {subscriptionId} exists.")
{
    public Guid SubscriptionId { get; } = subscriptionId;
}
```

**The error exposes the same data to the caller:**

```csharp
public sealed record SubscriptionNotFoundApiError : NotFoundApiError
{
    [JsonPropertyName("subscriptionId")]
    public required Guid SubscriptionId { get; init; }
}
```

**The mapping between them is explicit, one pair at a time:**

```csharp
internal static class SubscriptionErrorMappings
{
    public static SubscriptionNotFoundApiError ToApiError(this SubscriptionNotFoundException exception)
        => new()
        {
            Title = exception.Title,
            Detail = exception.Detail,
            SubscriptionId = exception.SubscriptionId,
        };
}
```

**A mapper wires the pairs into the handler:**

```csharp
internal sealed class SubscriptionExceptionMapper : IExceptionToApiErrorMapper
{
    public IApiError? Map(Exception exception) => exception switch
    {
        SubscriptionNotFoundException e => e.ToApiError(),
        SubscriptionLimitReachedException e => e.ToApiError(),
        _ => null,   // not ours — let the next mapper try
    };
}
```

```csharp
builder.Services.AddExceptionToApiErrorMapper<SubscriptionExceptionMapper>();
```

Mappers run in registration order and the first non-null result wins.

### Exceptions that share a hierarchy belong in one mapper

`ValidationException` derives from `BadRequestException`, so a general arm matched first would
swallow it and the response would lose the `errors` map. **Inside a single switch the compiler
prevents this** — the later arm is unreachable and the build fails with CS8510:

```csharp
public IApiError? Map(Exception exception) => exception switch
{
    ValidationException e => e.ToApiError(),   // derived first
    BadRequestException e => e.ToApiError(),   // swap these two and the file no longer compiles
    _ => null,
};
```

**Across two mappers nothing catches it.** Register a bad-request mapper before a validation one and
the general mapper wins silently — a 400 that no longer says which field was wrong. Split mappers by
domain (subscriptions, billing), never by status code, and related exceptions stay in the same
switch where the compiler can see them.

The reverse mistake cannot happen at all: `ValidationApiError.Errors` is `required`, so nothing can
be turned into a validation error without the field breakdown to fill it with.

**Why the package ships no ready-made mapping.** A generic "any NotFoundException becomes a plain
404" would compile and look convenient, but it throws away exactly what makes the error useful —
which subscription, which field, which limit. The base types decide the status code; the specifics
are the point, and they only survive if each pair is mapped deliberately.

A mapped exception is logged at **warning**, an unmapped one at **error** — the latter got past
every layer that should have caught it.

Note that this does not replace declaring return types on endpoints. The declaration is what
generates OpenAPI; the handler only ensures nothing escapes unshaped.

## Fields

| Field | Type | Description |
|---|---|---|
| `type` | `string?` | RFC URI, defaulted by each base type and overridable per project |
| `title` | `string` | **required**, non-blank — short summary, invariant across occurrences |
| `status` | `int?` | HTTP status code, filled in when the response is built |
| `detail` | `string` | **required**, non-blank — explanation specific to this occurrence |
| `instance` | `string?` | URI of the specific occurrence, typically the request path |
| `traceId` | `string?` | W3C Trace Context trace id |
| `errors` | `IReadOnlyDictionary<string, string[]>` | *(ValidationApiError)* **required**, non-empty — field-level errors |
| `retryAfterSeconds` | `int` | *(RateLimitApiError)* **required**, positive — mirrors the `Retry-After` header |
| `limit` | `int` | *(RateLimitApiError)* **required**, positive — permits per window |
| `windowSeconds` | `int` | *(RateLimitApiError)* **required**, positive — window duration |

### Required is checked, not just declared

`required` forces the initialiser to be *written*, not to contain anything. `Title = null!` compiles,
a nullable variable only warns, and `"title": null` deserialises without complaint. Every required
field therefore validates in its `init` accessor, so an error in an invalid state cannot be built at
all — the throw carries a stack trace pointing at the line that got it wrong, long before a response
goes out.

Blank counts as missing: a `title` of `"   "` is as useless to the caller as none, and an empty
`errors` map produces a 400 that promises to name the offending fields and then names none. Use
`BadRequestApiError` when there is no breakdown to give.

`with` expressions copy the backing fields directly rather than replaying the accessors, so enriching
an error costs nothing and cannot trip the guards.

## Notes

- **Set `status` yourself only when overriding.** `ToApiErrorResult()` derives it from the problem
  type, so you normally leave it alone. Setting it explicitly wins — useful for cases like a 410
  returned from a `NotFoundApiError`.
- **Constructing the response by hand still works.** `Results.Json(problem, statusCode, contentType)`
  behaves as before; then `status` really is yours to fill in, and yours to keep in step.
- **`traceId` fills itself in** from `Activity.Current`, both from `ToApiErrorResult()` and from the
  exception handler. That is the whole point of the extension field: a support request quoting the
  trace id leads straight to the trace. Set it by hand only when building the response yourself.
- Problems are `record` types, so equality is by value — convenient in tests.
- `type` is a stable identifier, not a link users are expected to follow. Overriding it with a
  project URI is fine and often better than pointing at the RFC.

## Licence

MIT — see [LICENSE](LICENSE).
