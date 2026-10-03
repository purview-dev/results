# Composing with Value Objects and ZodSharp

`Purview.Results` composes with [Purview.ValueObjects](https://www.nuget.org/packages/Purview.ValueObjects) and
[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) without depending on either. This page shows
the end-to-end shape: a value object's invariants are validated by ZodSharp, the outcome becomes an ordinary
result failure, and the failure is rendered by the ZodSharp.AspNetCore mapping.

## Why the composition works

- **ZodSharp owns the rules and the validation result.** `Validate` never throws; it returns a
  `ValidationResult<T>` carrying the value or every `ValidationError`.
- **ValueObjects owns the invariants.** A `[Scalar]` or `[ValueObject]` type is the only place its own
  construction is guarded.
- **Purview.Results owns the outcome.** `Purview.Results.ZodSharp.ToResult` turns the validation result into a
  `Result<TValue, TError>` whose error is one of the union's cases, so a rejected input flows through the same
  pipeline as every other expected outcome.

`Purview.Results` stays dependency-free: the integration packages depend on it, never the reverse, so a
consumer opts in by referencing the packages it needs.

## Type-level rules and the error identity

ZodSharp rules own their error identity. A rule that validates a value object *as a unit* can implement
`ZodSharp.Core.IZodRule` to report its own `Code` and `Origin` — which is what lets the same attribute produce a
different code per annotated scalar, and what makes an **origin** a rule the HTTP layer can key on:

```csharp
using ZodSharp.Core;
using Purview.ValueObjects;   // IScalarValueObject<TSelf, TValue>

public readonly record struct NonEmptyRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, Guid>
{
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

    public string GetErrorMessage(in TSelf value) => Message ?? MessageFormat;

    string IValidationRule<TSelf>.Code => Code ?? ErrorCode;

    string? IZodRule.Code => Code;
    string? IZodRule.Origin => "value_object";
}

[ZodRule(typeof(NonEmptyRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NonEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
```

Applying the attribute to a scalar makes its generated `Create` reject the sentinel value, reporting the rule's own
code and origin (`Code = "invalid_asset_id"`, `Origin = "value_object"`, empty path), while `Hydrate` stays
replay-safe:

```csharp
[Scalar]
[ZodSchema]
[NonEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; }
}
```

## The rejection as a result failure

The case that carries the errors implements `IValidationErrorCarrier`, so the HTTP layer can render it without
knowing the error type:

```csharp
[GenerateResult]
readonly union TenantError(TenantInputInvalid, TenantAlreadyExists);

readonly record struct TenantInputInvalid(TenantInput Input, ImmutableArray<ValidationError> Errors)
    : IValidationErrorCarrier
{
    ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}
```

`ToResult` maps the validation outcome into that union, naming the error type explicitly because a union case does
not carry the union that contains it:

```csharp
return TenantInputSchema
    .Validate(input)
    .ToResult<TenantInput, TenantError>(errors => new TenantInputInvalid(input, errors))
    .Bind(RegisterValidated);
```

## Rendering it, keyed by the rule's identity

`Purview.Results.ZodSharp.AspNetCore` renders the carrier as a validation problem, and the rules registered on
`ZodResultsHttpOptions` can answer by code, category **or origin**:

```csharp
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddResultsHttp();
builder.Services.AddResultsZodSharpHttp(options => options
    // One code a schema can report.
    .MapCode("invalid_asset_id", StatusCodes.Status422UnprocessableEntity)
    // Every type-level rule failure, whatever its code, because the rule owns Origin = "value_object".
    .MapOrigin("value_object", StatusCodes.Status422UnprocessableEntity)
);
```

Because the origin is the axis a rule owns, `MapOrigin("value_object", …)` answers every type-level rule failure —
including future rules — while the `MapCode` rule still narrows the one code that deserves its own status. A code
is narrower than a category, which is narrower than an origin, so precedence is structural rather than
registration-ordered; see [ZodSharp Problem Details](ZodSharp-ProblemDetails.md).

## Runnable example

[`Examples.ValueObjects.Zod`](https://github.com/purview-dev/results/tree/main/src/src/Examples.ValueObjects.Zod)
runs the whole composition: a `[Scalar]` tenant identifier whose type-level `[ZodRule]` owns
`Code = "invalid_tenant_id"` and `Origin = "value_object"`, validated into a `Result<Tenant, TenantError>`
failure and answerable by `MapOrigin("value_object", …)`.

```bash
dotnet run --project src/src/Examples.ValueObjects.Zod
```

## Related

- [ZodSharp Integration](ZodSharp-Integration.md) — producing results that carry validation errors.
- [ZodSharp Problem Details](ZodSharp-ProblemDetails.md) — the per-code, per-category and per-origin rules.
- [Union Errors](Union-Errors.md) — the error union the rejected input becomes a case of.