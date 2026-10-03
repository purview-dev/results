# Purview.Results.ZodSharp

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.ZodSharp.svg)](https://www.nuget.org/packages/Purview.Results.ZodSharp)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

Bridges [ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) `ValidationResult<T>` values into
[`Purview.Results`](https://www.nuget.org/packages/Purview.Results), so validation outcomes flow through the
same result pipeline as every other expected outcome instead of throwing.

## Installation

```bash
dotnet add package Purview.Results.ZodSharp
```

## Quick start

ZodSharp validation never throws: `Validate` returns a `ValidationResult<T>` carrying the validated value on
success and every `ValidationError` on failure. `ToResult` translates that into a result whose error type the
caller chooses:

```csharp
return RepositoryReconciliationResultSchema
    .Validate(reconciliationResult)
    .ToResult<RepositoryReconciliationResult, ReconciliationError>(errors =>
        new ReconciliationResultInvalid(reconciliationResult, errors)
    );
```

When the surrounding method's success type differs from the validated type, produce the failure with the
generated `AsFailure<TValue>()` helper instead, because the validated value is not the value the method
succeeds with:

```csharp
var validated = ProviderConnectionIdSchema.Validate(providerConnectionId);

if (!validated.IsSuccess)
    return new ProviderConnectionInvalid(providerConnectionId, validated.Errors)
        .AsFailure<RepositoryReconciliationResult>();

return await ReconcileCoreAsync(validated.Value, repositories, cancellationToken);
```

## API

| Member | Purpose |
| --- | --- |
| `ValidationResult<TValue>.ToResult<TValue, TError>(Func<ImmutableArray<ValidationError>, TError> onFailure)` | Success carries the validated value; failure carries the created error. `onFailure` runs only when validation failed, so a successful validation allocates no error |
| `IValidationErrorCarrier` | Implemented by an error value that carries ZodSharp validation errors, so an HTTP layer can turn it into a validation problem without knowing the error type |

The error type must be named explicitly when it is a union, as in
`ToResult<RepositoryReconciliationResult, ReconciliationError>(...)`, because a union case does not carry the
union type that contains it.

A factory returning a result rather than an error is deliberately **not** offered: for a lambda returning
`Result<TValue, TError>` the compiler prefers a `Func<..., TError>` parameter and would silently nest the
results. Naming the error type explicitly keeps the intent unambiguous.

## Examples

[`src/src/Examples.Zod`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Zod) is a
runnable console example that validates a `[ZodSchema] TenantInput` and turns the outcome into a
`Result<Tenant, TenantError>`, with the rejection carrying its reported `ValidationError`s.

```bash
dotnet run --project src/src/Examples.Zod
```

The [repository README](https://github.com/purview-dev/results#examples) lists the Basic, ASP.NET Core,
ASP.NET Core + Zod and value-objects composition examples too.

## Related packages

| Package | Purpose |
| --- | --- |
| [`Purview.Results.ZodSharp.AspNetCore`](https://www.nuget.org/packages/Purview.Results.ZodSharp.AspNetCore) | Renders the validation errors an `IValidationErrorCarrier` error carries as `HttpValidationProblemDetails` |

## Agent skills

This package ships the `purview-results-zodsharp-validation` agent skill under `.agents/`. Repositories that
import `Purview.BuildSdk` get it mirrored into their own `.agents/` folder on the next restore or build, so AI
agents working there receive the guidance automatically.

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
