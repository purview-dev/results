# Purview.Results.AspNetCore

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.AspNetCore.svg)](https://www.nuget.org/packages/Purview.Results.AspNetCore)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

Maps [`Purview.Results`](https://www.nuget.org/packages/Purview.Results) values onto ASP.NET Core responses, so
an endpoint can return `Result<TValue, TError>` and let the host decide what each error case looks like on the
wire.

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

app.MapGet("/tenants/{id:int}", (int id) => GetTenant(id)).WithResultsHttp();
```

The handler returns a `Result<Tenant, TenantError>`; the filter converts it into the configured response.
`AddResultsHttp` also registers problem-details services (`AddProblemDetails()`), so the unmapped-failure and
uninitialized-result paths work without further host setup.

## How a result becomes a response

**Success** — the value is serialized with `SuccessStatusCode` (`200 OK` by default). A result whose successful
value is itself an `IResult` is passed through untouched, and `SuccessMapper` overrides both when set.

**Failure** — the error is resolved to its *case* value (the active case of a union error, or the error itself
for a non-union error), then mapped in this order:

1. a mapping registered for the case type — `Map<TenantNotFound>(...)`
2. a mapping registered for the error type — `Map<TenantError>(...)`, which handles every case without its own
   mapping
3. the fallback stage, in registration order: the `AddFallback(...)` delegates and the
   `AddFailureMapper<TMapper>()` mappers share one list, and whatever is registered first is consulted first; a
   fallback or mapper returns `null` to defer to the next entry
4. a `ProblemDetails` response using `UnmappedStatusCode` (`500`), `UnmappedTitle`, and an `errorType`
   extension naming the unmapped case — or an `InvalidOperationException` when `ThrowOnUnmappedFailure` is set

An **uninitialized** result (`default`) is logged and answered with the unmapped-failure response, because an
endpoint returning `default` is a host bug rather than a domain outcome.

## Options

| Option | Default | Purpose |
| --- | --- | --- |
| `SuccessStatusCode` | `200` | Status code for a serialized successful value |
| `SuccessMapper` | `null` | Replaces the default success handling entirely |
| `UnmappedStatusCode` | `500` | Status code for a failure with no mapping |
| `UnmappedTitle` | *"The operation failed with an error that is not mapped to an HTTP response."* | Title of the unmapped `ProblemDetails` |
| `ThrowOnUnmappedFailure` | `false` | Throw instead of producing a problem response; useful during development |
| `IncludeTraceId` | `true` | Whether problem responses this package writes itself carry the request trace identifier |
| `Map<TCase>(Func<TCase, IResult>)` | — | Maps a case (or the error itself) to a response |
| `Map<TCase>(Func<TCase, HttpContext, IResult>)` | — | Same, with access to the request |
| `AddFallback(Func<object?, HttpContext, IResult?>)` | — | Consulted in order for unmapped failures, with the case value |
| `AddFailureMapper<TMapper>()` | — | Same stage, for a mapper class resolved from dependency injection |

Registering the same type twice replaces the earlier mapping.

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

When a filter is not appropriate, convert explicitly:

```csharp
app.MapGet("/tenants/{id:int}", (int id, HttpContext context) =>
    GetTenant(id).ToHttpResult(context));
```

`ToHttpResult(HttpContext)` resolves `IResultsHttpMapper` from the request services; the
`ToHttpResult(IResultsHttpMapper, HttpContext)` overload takes one directly.

## Extensibility

`IResultsHttpMapper` is registered with `AddResultsHttp` as
`DefaultResultsHttpMapper` via `TryAddSingleton`, so a host can register its own implementation first to replace
the defaults entirely. A host that replaces it also bypasses `ResultsHttpOptions` — including the failure mappers
— so prefer the extension points above unless the pipeline itself has to change.

## Examples

[`src/examples/Examples.AspNetCore`](https://github.com/purview-dev/results/tree/main/src/examples/Examples.AspNetCore)
is a runnable minimal-API example that maps the `TenantNotFound` case to `404`, the `TenantDisabled` case to
`403` and the `TenantError` error type to `409`, and shows the response an endpoint that returns `default`
receives.

```bash
dotnet run --project src/examples/Examples.AspNetCore --urls http://localhost:5215
```

The [repository README](https://github.com/purview-dev/results#examples) lists the Basic, ZodSharp and
ASP.NET Core + Zod examples too.

## Related packages

Validation failures that carry ZodSharp errors are rendered as `HttpValidationProblemDetails` by
[`Purview.Results.ZodSharp.AspNetCore`](https://www.nuget.org/packages/Purview.Results.ZodSharp.AspNetCore).
This package deliberately knows nothing about ZodSharp.

## Agent skills

This package ships the `purview-results-http-mapping` agent skill under `.agents/`. Repositories that import
`Purview.BuildSdk` get it mirrored into their own `.agents/` folder on the next restore or build, so AI agents
working there receive the guidance automatically.

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
