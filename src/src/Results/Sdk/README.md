# Purview.Results

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.svg)](https://www.nuget.org/packages/Purview.Results)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

A small, dependency-light `Result<TValue, TError>` for .NET that lets **expected** failures flow through a
value instead of an exception. Exceptional circumstances still throw; states you expect — not found, invalid,
conflict — are values.

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
| `MapError(map)` | Transform the error, preserving the value |
| `ToString()` | `Success(value)`, `Failure(error)` or `Uninitialized` |

`Result<TValue, TError>` is a `readonly record struct`. `default` is *uninitialized*: `IsInitialized`,
`IsSuccess` and `IsFailure` are all `false`, and `Value`, `Error`, `Match`, `Map`, `Bind` and `MapError` throw
`InvalidOperationException` rather than silently treating the result as a failure or a success.

`IResultValue` exposes a non-generic, read-only view (`IsInitialized`, `IsSuccess`, `SuccessValue`,
`ErrorValue`) for infrastructure that cannot be generic over the value and error types — an ASP.NET Core
endpoint filter, for example. Its accessors never throw: the one that does not describe the current state
returns `null`.

## Union error types

A C# 15 union is a natural `TError`: the error cases stay strongly typed. Because C# never composes the union
conversion with the result's own conversion, a case value cannot be returned directly — the source generator
bundled in this package generates a per-case `AsFailure<TValue>()` helper for `[GenerateResult]` unions:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => new TenantNotFound(tenantId).AsFailure<Tenant>();
```

The generator cannot declare that conversion for you: C# forbids user-defined operators in a static class,
forbids conversion operators in extension members, and permits only one user-defined conversion per conversion
sequence. The same package ships a code fix that offers the rewrite in the IDE when a case value is returned
where a result is expected.

If you prefer to avoid the helper entirely, the cast form needs no generated code, because the cast closes the
case → union conversion so only the library's union → result conversion remains:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => (TenantError)new TenantNotFound(tenantId);
```

## Examples

[`src/src/Examples.Basic`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Basic) is a
runnable console example over the Tenant* domain this README documents: the three result states, the combinators,
probing, the throw-on-misuse contract and the generated `AsFailure<TValue>()` helper.

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
