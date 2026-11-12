# Purview.Results.AspNetCore

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.AspNetCore.svg)](https://www.nuget.org/packages/Purview.Results.AspNetCore)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

Maps [`Purview.Results`](https://www.nuget.org/packages/Purview.Results) values onto ASP.NET Core responses, so
an endpoint can return `Result<TValue, TError>` or a value-less `Result<TError>` and let the host decide what
each error case looks like on the wire.

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

The handler returns a `Result<Tenant, TenantError>`; the filter converts it into the configured response.
`AddResultsHttp` also registers problem-details services (`AddProblemDetails()`), so the unmapped-failure and
uninitialized-result paths work without further host setup.

## How a result becomes a response

**Success** — the value is serialized with `SuccessStatusCode` (`200 OK` by default). A result whose successful
value is itself an `IResult` is passed through untouched, and `SuccessMapper` overrides both when set. A
successful unit `Result<TError>` carries no payload, so it answers `204 No Content` unless `SuccessMapper` is
set.

**Failure** — the error is resolved to its *case* value (the innermost active case of a union error, so a nested
union resolves to its leaf; or the error itself for a non-union error), then mapped in this order:

1. a mapping registered for the most specific case type — `Map<TenantNotFound>(...)`
2. a mapping registered for each enclosing union type, from the inside out — `Map<BillingError>(...)` handles a
   whole nested union when its leaf has no mapping of its own
3. a mapping registered for the error type — `Map<TenantError>(...)`, which handles every case without its own
   mapping
4. the fallback stage, in registration order: the `AddFallback(...)` delegates and the
   `AddFailureMapper<TMapper>()` mappers share one list, and whatever is registered first is consulted first; a
   fallback or mapper receives the leaf case and returns `null` to defer to the next entry
5. a `ProblemDetails` response using `UnmappedStatusCode` (`500`) and `UnmappedTitle` — or an
   `InvalidOperationException` when `ThrowOnUnmappedFailure` is set. The unmapped case's type name is always
   logged, but it is written into the response's `errorType` extension only when
   `IncludeErrorTypeInProblemDetails` is set, because it discloses internal namespaces to the caller.

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
`Map<ISomeInterface>` never matches; answer an interface or base-type shape with a failure mapper. The options
are startup configuration: they are frozen the first time a request reads them.

## Choosing an extension point

| The rule needs… | Use |
| --- | --- |
| One answer per error or case **type** | `Map<TCase>(...)` |
| The **value** the failure carries — a validation code, a category, a field | `IResultsFailureMapper` via `AddFailureMapper<TMapper>()` |
| A quick inline rule, with no dependencies | `AddFallback((error, context) => ...)` |
| To replace the whole pipeline | Your own `IResultsHttpMapper` (see [Extensibility](#extensibility)) |

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

The mapper is resolved from the request's services the first time it is needed, so it may take its own
dependencies in its constructor. Register it (`AddSingleton<MyMapper>()`, or against `IResultsFailureMapper` and
name that interface as `TMapper`) before the first request; a failure that reaches an unregistered mapper throws
an `InvalidOperationException` naming the registration that is missing.

## Converting a result by hand

When a filter is not appropriate, convert explicitly. `ToHttpResult()` wraps the result in a `ResultHttpResult`,
an `IResult` that is mapped by the registered `IResultsHttpMapper` when it executes, so the response is exactly the
one `WithResultsHttp()` produces:

```csharp
app.MapGet("/tenants/{id}", (string id) => GetTenant(new TenantId(id)).ToHttpResult());
app.MapDelete("/tenants/{id}", (string id) => DeleteTenant(new TenantId(id)).ToHttpResult());   // 204 on success
```

It is available for both `Result<TValue, TError>` and the unit `Result<TError>`, and it describes the success
response to OpenAPI (`TValue` with `SuccessStatusCode`, or `204` for a unit result). `ToHttpResult(HttpContext)`
maps immediately with the mapper resolved from the request services, and `ToHttpResult(IResultsHttpMapper,
HttpContext)` takes one directly.

## Native AOT

The package is `IsAotCompatible`, and a successful value reaches `TypedResults` with its static type (`Ok<TValue>`,
never `Ok<object>`). Two things are needed in a Native AOT host:

- **Return `result.ToHttpResult()` from the handler, not the result.** Under `PublishAot` ASP.NET Core's request
  delegate generator resolves JSON metadata for every handler's declared return type when it builds the endpoints,
  and a `Result<...>` has none — it is never serialized, and a union error cannot be described by System.Text.Json's
  source generator (`SYSLIB1227`). A handler that returns the result directly therefore fails every request with a
  `NotSupportedException` at startup; `WithResultsHttp()` runs too late to prevent it.
- **Put the value types in your `JsonSerializerContext`**, with the request body types:
  `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));`.
  The problem responses resolve from ASP.NET Core's own context.

Set the options in code (the configuration binder needs reflection unless its source generator is enabled), and if
you set `SuccessMapper`, cast its `object?` value to the value type before serializing it.

## Observability

The mapper counts under the meter `ResultsHttpTelemetry.MeterName` (`Purview.Results.AspNetCore`):
`purview.results.failures.mapped`, `purview.results.failures.unmapped` and `purview.results.uninitialized`, each
tagged `purview.results.error_type` (the case type name, or `unknown` for an uninitialized result), which is also
added to `Activity.Current`. Register it with `metrics.AddMeter(ResultsHttpTelemetry.MeterName)`. Log statements
carry stable event ids: `1000` for an uninitialized result and `1001` for an unmapped failure.

## Extensibility

`IResultsHttpMapper` is registered with `AddResultsHttp` as
`DefaultResultsHttpMapper` via `TryAddSingleton`, so a host can register its own implementation first to replace
the defaults entirely. A host that replaces it also bypasses `ResultsHttpOptions` — including the failure mappers
— so prefer the extension points above unless the pipeline itself has to change.

## Examples

[`src/src/Examples.AspNetCore`](https://github.com/purview-dev/results/tree/main/src/src/Examples.AspNetCore)
is a runnable minimal-API example that maps the `TenantNotFound` case to `404`, the `TenantDisabled` case to
`403` and the `TenantError` error type to `409`. Its billing endpoint returns a union that nests `TenantError`
and `BillingError`, so it also shows a nested union resolving to its leaf (the billing cases map to `503` and
`402`) while the `TenantError` mapping still covers a nested tenant failure. Its `DELETE` endpoint returns a
unit `Result<TenantError>`, so a success answers `204` while a missing tenant still maps to `404`. It shows the
response an endpoint that returns `default` receives too.

```bash
dotnet run --project src/src/Examples.AspNetCore --urls http://localhost:5215
```

The [repository README](https://github.com/purview-dev/results#examples) lists the Basic, ZodSharp,
ASP.NET Core + Zod and Blazor WebAssembly/Server examples too.

## Related packages

Validation failures that carry ZodSharp errors are rendered as `HttpValidationProblemDetails` by
[`Purview.Results.ZodSharp.AspNetCore`](https://www.nuget.org/packages/Purview.Results.ZodSharp.AspNetCore).
This package deliberately knows nothing about ZodSharp.

## Agent skills

The `purview-results-http-mapping` agent skill covers this package. No package ships skills: install them, with every other Purview.Results skill, from the git repository with the skills CLI:

```bash
npx skills add purview-dev/results
```

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
