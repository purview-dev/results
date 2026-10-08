# ZodSharp Integration

`Purview.Results.ZodSharp` bridges [ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp)
`ValidationResult<T>` values into results, so validation outcomes flow through the same result pipeline as every
other expected outcome instead of throwing.

## Installation

```bash
dotnet add package Purview.Results.ZodSharp
```

## Why a bridge

ZodSharp validation never throws: `Validate` returns a `ValidationResult<T>` carrying the validated value on
success and every `ValidationError` on failure. That is already a result-shaped value, but it is not
`Result<TValue, TError>` — the failure type is fixed rather than chosen by the caller. `ToResult` closes that gap.

## Quick start

```csharp
return RepositoryReconciliationResultSchema
    .Validate(reconciliationResult)
    .ToResult<RepositoryReconciliationResult, ReconciliationError>(errors =>
        new ReconciliationResultInvalid(reconciliationResult, errors)
    );
```

`ToResult` names both type arguments explicitly — including the **error** type — because a union case does not
carry the union type that contains it:

```csharp
.ToResult<RepositoryReconciliationResult, ReconciliationError>(...)
```

## When the success type differs from the validated type

If the surrounding method's success value is not the validated value, produce the failure with the generated
`AsFailure<TValue>()` helper instead, because the validated value cannot be carried forward:

```csharp
var validated = ProviderConnectionIdSchema.Validate(providerConnectionId);

if (!validated.IsSuccess)
    return new ProviderConnectionInvalid(providerConnectionId, validated.Errors)
        .AsFailure<RepositoryReconciliationResult>();

return await ReconcileCoreAsync(validated.Value, repositories, cancellationToken);
```

## Validating without producing a value

When the method reports only why an input was rejected, `ToUnitResult` discards the validated value and returns a
unit `Result<TError>`. It still names the validated value type — a union case does not carry its union — but the
success holds the `Success` marker rather than the value:

```csharp
Result<TenantError> Validate(TenantInput input) =>
    TenantInputSchema
        .Validate(input)
        .ToUnitResult<TenantInput, TenantError>(errors => new TenantInputInvalid(input, errors));
```

Use `ToResult` when the success has to carry the validated value, and `ToUnitResult` when it does not.

## Async validation

`ValidateAsync` — on the generated schema, the `{Type}SchemaValidator` DI adapter, or a
`[ZodSchema(CustomValidationMethodName = ...)]` rule — returns a `ValueTask<ValidationResult<T>>`.
`ToResultAsync` and `ToUnitResultAsync` fold that into a result exactly as the synchronous methods fold `Validate`:

```csharp
async Task<Result<TenantError>> Validate(TenantInput input, CancellationToken cancellationToken) =>
    await TenantInputSchemaValidator
        .ValidateAsync(input, cancellationToken)
        .ToUnitResultAsync<TenantInput, TenantError>(errors => new TenantInputInvalid(input, errors));
```

The error type is named explicitly for the same reason as the synchronous forms, and the failure factory runs only
when validation failed.

## Carrying validation errors in the error value

`IValidationErrorCarrier` is implemented by an error value that carries ZodSharp validation errors, so an HTTP
layer can turn it into a validation problem without knowing the error type:

```csharp
public readonly record struct TenantInputInvalid(TenantInput Input, ImmutableArray<ValidationError> Errors)
    : IValidationErrorCarrier
{
    public ImmutableArray<ValidationError> ValidationErrors => Errors;
}
```

`IValidationErrorCarrier` exposes a single `ValidationErrors` member, preserving each `ValidationError`'s code,
origin, category, path and parameters. The **origin** is the structured origin a rule owns (`"value_object"` for a
type-level rule, `"array"` for a collection rule), which is how an HTTP layer can answer a whole family of rules
without naming each code.

[ZodSharp Problem Details](ZodSharp-ProblemDetails.md) consumes the interface; without it, a host would have to
map every validation-carrying error individually.

## API

| Member | Purpose |
| --- | --- |
| `ValidationResult<TValue>.ToResult<TValue, TError>(Func<ImmutableArray<ValidationError>, TError> onFailure)` | Success carries the validated value; failure carries the created error. `onFailure` runs only when validation failed, so a successful validation allocates no error |
| `ValidationResult<TValue>.ToUnitResult<TValue, TError>(Func<ImmutableArray<ValidationError>, TError> onFailure)` | The value-discarding counterpart: success is a unit `Result<TError>`, failure carries the created error |
| `ValueTask<ValidationResult<TValue>>.ToResultAsync<TValue, TError>(Func<ImmutableArray<ValidationError>, TError> onFailure)` | The asynchronous counterpart, for a schema whose validation awaits |
| `ValueTask<ValidationResult<TValue>>.ToUnitResultAsync<TValue, TError>(Func<ImmutableArray<ValidationError>, TError> onFailure)` | The value-discarding asynchronous counterpart |
| `IValidationErrorCarrier` | Implemented by an error value that carries ZodSharp validation errors |

A factory returning a **result** rather than an error is deliberately not offered: for a lambda returning
`Result<TValue, TError>` the compiler prefers a `Func<..., TError>` parameter and would silently nest the results.
Naming the error type explicitly keeps the intent unambiguous.

## Example

```bash
dotnet run --project src/src/Examples.Zod
```

`Examples.Zod` validates a `[ZodSchema] TenantInput` and turns the outcome into a `Result<Tenant, TenantError>`,
with the rejection carrying its reported `ValidationError`s.

## Related

- [ZodSharp Problem Details](ZodSharp-ProblemDetails.md) — the ASP.NET Core rendering of an `IValidationErrorCarrier`
  failure.
- [Core Concepts](Core-Concepts.md) — the result states the validated value flows into.
