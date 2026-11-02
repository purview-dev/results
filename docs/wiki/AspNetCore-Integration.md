# ASP.NET Core Integration

`Purview.Results.AspNetCore` maps a `Result<TValue, TError>` or a value-less `Result<TError>` onto an ASP.NET Core
response, so an endpoint can return a result and let the host decide what each error case looks like on the wire.

## Installation

```bash
dotnet add package Purview.Results.AspNetCore
```

## Quick start

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResultsHttp(options => options
    .Map<TenantNotFound>(error => TypedResults.NotFound())
    .Map<TenantDisabled>(error => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden))
    .Map<TenantError>(error => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict))   // every unmapped case
    .AddFallback((error, context) => error is null ? null : TypedResults.Problem())
);

var app = builder.Build();

app.MapGet("/tenants/{id}", (string id) => GetTenant(new TenantId(id))).WithResultsHttp();
```

`AddResultsHttp` also registers problem-details services (`AddProblemDetails()`), so the unmapped-failure and
uninitialized-result paths work without further host setup. It uses `TryAddSingleton` for `IResultsHttpMapper` and
`ResultsEndpointFilter`, so a host can register its own mapper first and replace the defaults.

`WithResultsHttp()` exists on both `RouteHandlerBuilder` and `RouteGroupBuilder`. The endpoint filter is
non-invasive: a handler that returns something other than an `IResultValue` is left untouched.

## How a result becomes a response

**Success** — the value is serialized with `SuccessStatusCode` (`200 OK` by default). A result whose successful
value is itself an `IResult` is passed through untouched, and `SuccessMapper` overrides both when set. A
successful unit `Result<TError>` carries no payload, so it answers `204 No Content` unless `SuccessMapper` is set.

**Failure** — the error is resolved to its *case* value (the innermost active case of a union error, so a nested
union resolves to its leaf; or the error itself for a non-union error), then mapped in this order:

1. a mapping registered for the most specific **case** type — `Map<TenantNotFound>(...)`
2. a mapping registered for each enclosing **union** type, from the inside out — `Map<BillingError>(...)` handles a
   whole nested union when its leaf has no mapping of its own
3. a mapping registered for the **error** type — `Map<TenantError>(...)`, which handles every case without its own
   mapping
4. the **fallback stage**, in registration order: `AddFallback(...)` delegates and `AddFailureMapper<TMapper>()`
   mappers share one ordered list; a fallback or mapper receives the leaf case and returns `null` to defer to the
   next entry
5. a `ProblemDetails` response using `UnmappedStatusCode` (`500`) and `UnmappedTitle` — or an
   `InvalidOperationException` when `ThrowOnUnmappedFailure` is set. The case's type name is always logged, but it
   is written into the response's `errorType` extension only when `IncludeErrorTypeInProblemDetails` is set,
   because it discloses internal namespaces to the caller

An **uninitialized** result (`default`) is logged and answered with the same `500`, always with
`errorType = "results_uninitialized"` (a fixed discriminator, not a type name), because an endpoint returning
`default` is a host bug rather than a domain outcome. `ThrowOnUnmappedFailure` does not apply to it.

## Options

| Option | Default | Purpose |
| --- | --- | --- |
| `SuccessStatusCode` | `200` | Status code for a serialized successful value |
| `SuccessMapper` | `null` | Replaces the default success handling entirely |
| `UnmappedStatusCode` | `500` | Status code for a failure with no mapping |
| `UnmappedTitle` | *"The operation failed with an error that is not mapped to an HTTP response."* | Title of the unmapped `ProblemDetails` |
| `ThrowOnUnmappedFailure` | `false` | Throw on an unmapped failure instead of producing a problem response; useful during development |
| `IncludeErrorTypeInProblemDetails` | `false` | Write the unmapped error's CLR type name into the `errorType` extension; enable per environment |
| `IncludeTraceId` | `true` | Whether problem responses this package writes itself carry the request trace identifier |
| `Map<TCase>(Func<TCase, IResult>)` | — | Maps a case (or the error itself) to a response |
| `Map<TCase>(Func<TCase, HttpContext, IResult>)` | — | Same, with access to the request |
| `AddFallback(Func<object?, HttpContext, IResult?>)` | — | Consulted in order for unmapped failures, with the case value |
| `AddFailureMapper<TMapper>()` | — | Same stage, for a mapper class resolved from dependency injection |

Registering the same type twice replaces the earlier mapping. A mapping matches the exact runtime type only, so
`Map<ISomeInterface>` never matches; answer an interface or base-type shape with a failure mapper. The options are
startup configuration: the mappings and fallbacks are frozen the first time a request reads them.

## Choosing an extension point

| The rule needs… | Use |
| --- | --- |
| One answer per error or case **type** | `Map<TCase>(...)` |
| The **value** the failure carries — a validation code, a category, a field | `IResultsFailureMapper` via `AddFailureMapper<TMapper>()` |
| A quick inline rule, with no dependencies | `AddFallback((error, context) => ...)` |
| To replace the whole pipeline | Your own `IResultsHttpMapper` |

A failure mapper is a **shape** rule, not a catch-all:

```csharp
public sealed class BlankIdentifierMapper : IResultsFailureMapper
{
    public IResult? Map(ResultsFailureContext context) =>
        context.Case is ITenantFailure { TenantId.Value: var id } && string.IsNullOrWhiteSpace(id)
            ? TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "An identifier is required.")
            : null;   // defer: the case mappings and the other fallbacks still apply
}

builder.Services.AddSingleton<BlankIdentifierMapper>();
builder.Services.AddResultsHttp(options => options
    .Map<TenantNotFound>(_ => TypedResults.NotFound())
    .AddFailureMapper<BlankIdentifierMapper>());
```

`ResultsFailureContext` carries the failure's **case** (the innermost active case of a union error, so a nested
union resolves to its leaf; or the error itself), the **error** the result carries, and the request. A mapper is
resolved from the failing request's services the first time it is needed, so it may take its own dependencies in its
constructor; register it before the first request. A failure that reaches an unregistered mapper throws an
`InvalidOperationException` naming the registration that is missing.

Answering every failure in a mapper — with a generic problem or a `202`, for example — turns a mapping gap in the
host into a plausible-looking response, which is exactly what the unmapped-failure path exists to expose. Map the
shapes you can name and return `null` for the rest.

## Converting a result by hand

`ToHttpResult()` wraps a result in a `ResultHttpResult`, an `IResult` that the registered `IResultsHttpMapper` maps
when it executes, so the response is exactly the one `WithResultsHttp()` produces and no filter is needed:

```csharp
app.MapGet("/tenants/{id}", (string id) => GetTenant(new TenantId(id)).ToHttpResult());
app.MapDelete("/tenants/{id}", (string id) => DeleteTenant(new TenantId(id)).ToHttpResult());   // 204 on success
```

It exists for both `Result<TValue, TError>` and the unit `Result<TError>`, and describes the success response to
OpenAPI: `TValue` as JSON with `SuccessStatusCode`, or `204` for a unit result (nothing when `SuccessMapper` is set).
`ToHttpResult(HttpContext)` maps immediately with the mapper resolved from the request services, and
`ToHttpResult(IResultsHttpMapper, HttpContext)` takes one directly.

## Native AOT

The package is `IsAotCompatible` and hands a successful value to `TypedResults` with its static type, so it binds
`Ok<TValue>`, never `Ok<object>`. A Native AOT host needs two things:

- **Return `result.ToHttpResult()`, not the result.** Under `PublishAot` the request delegate generator resolves
  JSON metadata for every handler's declared return type when it builds the endpoints. A `Result<...>` has none - it
  is never serialized, and System.Text.Json's source generator cannot describe a union error (`SYSLIB1227`) - so a
  handler that returns the result itself fails every request at startup, before `WithResultsHttp()` can run.
- **Put the value types in your `JsonSerializerContext`**, with the request body types. The problem responses resolve
  from ASP.NET Core's own context.

See [Blazor and Native AOT](Blazor-and-Native-AOT.md#a-native-aot-minimal-api) for a complete host.

## Observability

The mapper counts under the meter `ResultsHttpTelemetry.MeterName` (`Purview.Results.AspNetCore`):
`purview.results.failures.mapped`, `purview.results.failures.unmapped` and `purview.results.uninitialized`, each
tagged `purview.results.error_type` (the case type name, or `unknown` for an uninitialized result) and added to
`Activity.Current`. Register it with `metrics.AddMeter(ResultsHttpTelemetry.MeterName)`. Log statements carry
stable event ids: `1000` for an uninitialized result and `1001` for an unmapped failure.

## Extensibility

`IResultsHttpMapper` is registered with `AddResultsHttp` as `DefaultResultsHttpMapper` via `TryAddSingleton`, so a
host can register its own implementation first to replace the defaults entirely. A host that replaces it also
bypasses `ResultsHttpOptions` — including the failure mappers — so prefer `Map`, `AddFallback` and
`AddFailureMapper` unless the pipeline itself has to change.

## Example responses

Running `Examples.AspNetCore`:

| Request | Response |
| --- | --- |
| `GET /tenants/acme` | `200 OK` with the tenant |
| `GET /tenants/initech` | `404 Not Found` — the mapping for the `TenantNotFound` case |
| `GET /tenants/globex/usage` | `403 Forbidden` — the mapping for the `TenantDisabled` case |
| `POST /tenants/acme` | `409 Conflict` — the mapping for the `TenantError` error type |
| `DELETE /tenants/hooli` | `204 No Content` — a successful unit `Result<TenantError>` carries no payload |
| `DELETE /tenants/initech` | `404 Not Found` — the unit result's `TenantNotFound` failure maps by case |
| `GET /tenants/acme/billing` | `503 Service Unavailable` — the leaf `BillingServiceUnavailable` case of the nested `TenantOperationError` union |
| `GET /tenants/hooli/billing` | `402 Payment Required` — the leaf `BillingAccountMissing` case |
| `GET /tenants/initech/billing` | `404 Not Found` — a nested tenant failure resolves to the `TenantNotFound` leaf |
| `GET /tenants/globex/billing` | `403 Forbidden` — a nested tenant failure resolves to the `TenantDisabled` leaf |
| `GET /tenants/broken` | `500` with `errorType = "results_uninitialized"`, because an endpoint returning `default` is a host bug |
| `GET /explicit/tenants/initech` | `404 Not Found` — the same mapping, through `ToHttpResult()` instead of the filter |

```bash
dotnet run --project src/src/Examples.AspNetCore --urls http://localhost:5215
```

## Related

- [ZodSharp Problem Details](ZodSharp-ProblemDetails.md) — validation-carrying failures rendered as
  `HttpValidationProblemDetails`. This package deliberately knows nothing about ZodSharp.
- [Getting Started](Getting-Started.md) — the end-to-end walkthrough.
