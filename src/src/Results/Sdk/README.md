# Purview.Results

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.svg)](https://www.nuget.org/packages/Purview.Results)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

A small, dependency-light `Result<TValue, TError>` for .NET that lets **expected** failures flow through a
value instead of an exception. Exceptional circumstances still throw; states you expect — not found, invalid,
conflict — are values. A value-less `Result<TError>` covers operations that have nothing to return on success.

## Installation

```bash
dotnet add package Purview.Results
```

## Quick start

```csharp
using Purview.Results;

Result<Tenant, TenantError> GetTenant(TenantId tenantId) =>
    _tenants.TryGet(tenantId, out var tenant)
        ? Result<Tenant, TenantError>.Success(tenant)
        : Result<Tenant, TenantError>.Failure(new TenantNotFound(tenantId));

var result = GetTenant(tenantId);

if (result.IsSuccess)
    Console.WriteLine(result.Value.Name);

var message = result.Match(
    tenant => $"Found {tenant.Name}",
    error => $"Could not load the tenant: {error}"
);
```

Both member states are also reachable through implicit conversions, so an error value or a success value can be
returned directly from a method whose return type is the result:

```csharp
Result<int, string> ok = 42;
Result<int, string> failed = "not a number";
```

## API

| Member | Purpose |
| --- | --- |
| `Result<TValue, TError>.Success(value)` / `.Failure(error)` | Create a result |
| `Result.Success<TValue, TError>(value)` / `Result.Failure<TValue, TError>(error)` | Create a result without naming the value twice |
| `IsInitialized` | Whether the result was created at all |
| `IsSuccess` / `IsFailure` | Which state the result holds (`[MemberNotNullWhen]`, so nullability flows) |
| `Value` / `Error` | The payload; throws `InvalidOperationException` in the other state |
| `Match(success, failure)` | Fold both states into one value |
| `Map(map)` | Transform the successful value, preserving the error |
| `Bind(bind)` | Chain another operation that can fail, preserving the error type |
| `Bind(bind, mapError)` | Chain another operation that fails with a different error type, widening the existing error |
| `MapError(map)` | Transform the error, preserving the value |
| `Throw()` | Return the successful value, or throw `ResultException<TError>` carrying the error |
| `ToString()` | `Success(value)`, `Failure(error)` or `Uninitialized` |

`Result<TValue, TError>` is a `readonly record struct`. `default` is *uninitialized*: `IsInitialized`,
`IsSuccess` and `IsFailure` are all `false`, and `Value`, `Error`, `Match`, `Map`, `Bind` and `MapError` throw
`InvalidOperationException` rather than silently treating the result as a failure or a success.

`IResultValue` exposes a non-generic, read-only view (`IsInitialized`, `IsSuccess`, `SuccessValue`,
`ErrorValue`) for infrastructure that cannot be generic over the value and error types — an ASP.NET Core
endpoint filter, for example. Its accessors never throw: the one that does not describe the current state
returns `null`.

`Throw()` is the deliberate escape hatch: it returns the successful value and throws a `ResultException<TError>`
carrying the error on failure, so a boundary that must throw does not have to branch. The exception exposes the
error with its original type through `Error`; a non-generic `ResultException` base carries it as `object?` for a
catch-all. An uninitialized result is still a bug, so `Throw()` reports `default` with
`InvalidOperationException`.

## Unit results

An operation that has nothing to return on success — a command, a validation-only step — uses the value-less
`Result<TError>`. It has the same three states and the same throw-on-misuse contract, but its success holds the
`Success` marker rather than a value, so its success callbacks take no argument:

```csharp
Result<TenantError> DeleteTenant(TenantId tenantId) =>
    _tenants.Remove(tenantId)
        ? Result<TenantError>.Success()
        : new TenantNotFound(tenantId).AsFailure();

var deleted = DeleteTenant(tenantId);

deleted.Match(() => "deleted", error => $"could not delete: {error}");   // success takes no value
deleted.Map(() => 1);                                                    // attach a value to a success
deleted.Bind(() => GetTenant(tenantId));                                 // continue with another operation
deleted.Ensure(() => _tenants.Count > 0, () => new TenantError());       // turn a success into a failure
```

| Member | Purpose |
| --- | --- |
| `Result<TError>.Success()` / `.Failure(error)` | Create a unit result |
| `Result.Success<TError>()` / `Result.Failure<TError>(error)` | Create a unit result without naming the error twice |
| `IsInitialized` / `IsSuccess` / `IsFailure` | The three states, shared with `Result<TValue, TError>` |
| `Error` | The error; throws `InvalidOperationException` in the other state |
| `Match(success, failure)` / `Map(map)` / `Bind(bind)` / `MapError(map)` | Fold or transform; the success callbacks take no value |
| `Throw()` | Return the `Success` marker, or throw `ResultException<TError>` carrying the error |
| `ToString()` | `Success`, `Failure(error)` or `Uninitialized` |
| `Result<TValue, TError>.DiscardValue()` | Turn a value result into a unit result, preserving the error |
| `Result<TError>.MapAsync` / `.BindAsync` / widening `Bind` / `Ensure` / `Tap` / `Switch` / `OrElse` | The unit-result counterparts of the value-result extensions, in `ResultExtensions` |

The implicit conversions from `Success` and from `TError` are public contract, so a method returning
`Result<TError>` can `return` either the marker or a plain error.

## Union error types

A C# 15 union is a natural `TError`: the error cases stay strongly typed. Because C# never composes the union
conversion with the result's own conversion, a case value cannot be returned directly — the source generator
bundled in this package generates a per-case `AsFailure<TValue>()` helper for `[GenerateResult]` unions, plus a
non-generic `AsFailure()` that produces a unit result:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => new TenantNotFound(tenantId).AsFailure<Tenant>();
Result<TenantError> DeleteTenant(TenantId tenantId) => new TenantNotFound(tenantId).AsFailure();
```

The generator also declares a C# 14 extension block on the union type itself, so the union names the error:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => TenantError.Failure<Tenant>(new TenantNotFound(tenantId));
Result<TenantError> DeleteTenant(TenantId tenantId) => TenantError.Failure(new TenantNotFound(tenantId));
Result<TenantError> ok = TenantError.Success();
Result<Tenant, TenantError> created = TenantError.Success(tenant);
```

Use the union-receiver factory when a **leaf** case type is shared with another union: the per-case `AsFailure`
helper is generated once (for the union that sorts first) and reported as `RSG1006`, so it can bind to the wrong
union, while `Union.Failure(...)` always names the union you want. A case type that is itself a union is an
*included union*: sharing it is composition, so it is not reported, and the factory also covers the included
union's cases by building the nested value
(`RegisterTenantError.Failure<Tenant>(new BillingAccountMissing(id))`).

The generator cannot declare that conversion for you: C# forbids user-defined operators in a static class,
forbids conversion operators in extension members, and permits only one user-defined conversion per conversion
sequence. The same package ships a code fix that offers the rewrite in the IDE when a case value is returned
where a result is expected.

If you prefer to avoid the helper entirely, the cast form needs no generated code, because the cast closes the
case → union conversion so only the library's union → result conversion remains:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => (TenantError)new TenantNotFound(tenantId);
```

When one service calls another, widen the callee's error union into the caller's operation-family union with the
widening `Bind`, or lift it at a guard with the generated helper. List the callee's union as a case rather than its
leaf cases: the caller's factory then covers the callee's cases by constructing the nested value, and a leaf is
never shared. If a **leaf** case type must be shared, convert it with the union-receiver `Union.Failure(...)`
factory, which is generated for every union. A case reachable through two different included unions is reported as
`RSG1008` and skipped.

## Examples

[`src/src/Examples.Basic`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Basic) is a
runnable console example over the Tenant* domain this README documents: the three result states, the combinators,
probing, the throw-on-misuse contract, the generated `AsFailure<TValue>()` helper, chaining two services by
widening one error union into another, and `Throw()`.

```bash
dotnet run --project src/src/Examples.Basic
```

The [repository README](https://github.com/purview-dev/results#examples) lists the ZodSharp, ASP.NET Core,
ASP.NET Core + Zod and value-objects composition examples too.

## Related packages

| Package | Purpose |
| --- | --- |
| [`Purview.Results.AspNetCore`](https://www.nuget.org/packages/Purview.Results.AspNetCore) | Maps results onto ASP.NET Core responses, including `ProblemDetails` |
| [`Purview.Results.ZodSharp`](https://www.nuget.org/packages/Purview.Results.ZodSharp) | Bridges ZodSharp `ValidationResult<T>` into results |
| [`Purview.Results.ZodSharp.AspNetCore`](https://www.nuget.org/packages/Purview.Results.ZodSharp.AspNetCore) | Maps validation-carrying failures onto `HttpValidationProblemDetails` |

## Agent skills

This package ships the `purview-results-core` and `purview-results-union-errors` agent skills, the
`purview-results-union-author` agent and the `migrate-error-returns-to-result-unions` prompt under `.agents/`.
Repositories that import `Purview.BuildSdk` get them mirrored into their own `.agents/` folder on the next
restore or build, so AI agents working there receive the guidance automatically.

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
