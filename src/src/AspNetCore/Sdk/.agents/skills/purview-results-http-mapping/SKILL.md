---
name: purview-results-http-mapping
description: "Use when an ASP.NET Core endpoint returns Purview.Results values — registering AddResultsHttp, mapping error cases to status codes with Map<T>, using fallbacks, correcting unmapped-failure 500s, or converting a result with ToHttpResult."
---

# Mapping results onto ASP.NET Core responses

Use this skill when a minimal-API endpoint, or any handler, returns `Result<TValue, TError>` and the host has to
decide what each error case looks like on the wire.

## Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddResultsHttp(options => options
    .Map<TenantNotFound>(error => TypedResults.NotFound())
    .Map<TenantDisabled>(error => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "The tenant is disabled."))
    .Map<TenantError>(error => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict))  // every other case
);

app.MapGet("/tenants/{id:int}", (int id) => GetTenant(id)).WithResultsHttp();
```

- `AddResultsHttp` registers problem-details services, the mapper (`TryAddSingleton`, so a host can register its
  own first) and the endpoint filter. Its `configure` callback is optional.
- `WithResultsHttp()` exists on both `RouteHandlerBuilder` and `RouteGroupBuilder` and adds the endpoint filter.
  A handler that returns something other than an `IResultValue` is left untouched, so the filter is safe to
  apply to a whole group.
- Mapping by hand is also supported: `result.ToHttpResult(context)` resolves the mapper from the request
  services, and `result.ToHttpResult(mapper, context)` takes one directly.

## Response resolution order

**Success**

1. `SuccessMapper` when set — it replaces the default entirely.
2. A successful value that is itself an `IResult` is passed through untouched.
3. Otherwise the value is serialized: `200 OK` for the default status code, `TypedResults.Json(value,
   statusCode: …)` when `SuccessStatusCode` was changed.

**Failure** — the error is resolved to its *case* value (the active union case, or the error itself when it is
not a union), then:

1. the mapping registered for the **case** type — `Map<TenantNotFound>(…)`;
2. the mapping registered for the **error** type — `Map<TenantError>(…)`, which covers every case without its
   own mapping;
3. each registered **fallback**, in order; a fallback returns `null` to defer to the next one;
4. the **unmapped-failure** response.

Registering the same type twice replaces the earlier mapping. Fallbacks receive the *case* value, so a fallback
never has to unwrap the union itself.

## Options

| Option | Default | Notes |
| --- | --- | --- |
| `SuccessStatusCode` | `200` | Status for a serialized successful value |
| `SuccessMapper` | `null` | Replaces the default success handling |
| `UnmappedStatusCode` | `500` | Status for a failure with no mapping |
| `UnmappedTitle` | *"The operation failed with an error that is not mapped to an HTTP response."* | Title of the unmapped problem |
| `ThrowOnUnmappedFailure` | `false` | Throw instead of responding; use during development |
| `IncludeTraceId` | `true` | Whether problems this package writes carry the request trace identifier |

## Treat an unmapped failure as a bug in the host

An unmapped failure is a **mapping gap**, not a domain outcome: the response is `UnmappedStatusCode` (`500`)
with a `ProblemDetails` carrying an `errorType` extension naming the case, and an error is logged. When you see
one in the logs, add the mapping; do not widen a catch-all mapping to silence it.

The same path handles an **uninitialized** result (`default`), which is logged and answered with
`errorType = "results_uninitialized"` — an endpoint returning `default` is always a bug in the handler.

During development, `ThrowOnUnmappedFailure = true` turns both into an `InvalidOperationException` so the gap
surfaces at the call site.

## Choosing status codes

Map the *case*, not the HTTP verb:

| Domain meaning | Response |
| --- | --- |
| Input failed validation | `400` with a validation problem (see `purview-results-zodsharp-problems`) |
| Not authenticated / not authorised | `401` / `403` |
| The thing does not exist | `404` |
| The thing already exists | `409` |
| The thing is in the wrong state for this operation | `409` or `422` |
| Rate-limited or temporarily unavailable | `429` / `503` |

Keep the mapping table next to `AddResultsHttp` so every case is visible in one place; a case that is missing
from it is exactly what the unmapped-failure log is telling you.

## Pitfalls

- **Do not** return `IResult` from domain or application code — results stay transport-agnostic and the host
  keeps the mapping.
- **Do not** map a case to a status code you would not document; `500` for a domain outcome hides real bugs.
- A shared case type used by two unions can be mapped once — mapping is keyed by the case type.
- The unmapped `ProblemDetails` is written through `IProblemDetailsService`, so ASP.NET Core adds the trace
  identifier itself; `IncludeTraceId` only affects problems this package serializes itself.
