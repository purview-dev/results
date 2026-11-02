# Composing with Value Objects and ZodSharp

`Purview.Results` composes with [Purview.ValueObjects](https://www.nuget.org/packages/Purview.ValueObjects) and
[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) without depending on either. This page shows
the end-to-end shape: a value object's invariants are validated by ZodSharp, the outcome becomes an ordinary
result failure, and an ASP.NET Core host can render that failure with the ZodSharp.AspNetCore mapping. The
runnable example stops at the result — it is a console app with no HTTP layer — so the rendering section shows the
host registration on its own.

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

### Automatic and manual scalar forms

`[Scalar]` is the *manual* form — you declare the underlying property. The *automatic* form, `[Scalar<Guid>]`
(or `[Scalar(typeof(Guid))]`), has the value-object generator declare `public Guid Value { get; init; }`, and
Purview.ZodSharp (the 3.x line this repository builds against) generates the schema from that attribute even though the member is not visible to it:

```csharp
[Scalar<Guid>]
[ZodSchema]
[NonEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId;
```

The automatic form is what a generator-declared property requires anyway: a property-level DataAnnotation has no
member to attach to, so the invariant has to be a **type-level** rule. Keep the manual form when property-level
DataAnnotations are needed. The two forms are mutually exclusive — declaring the generator-owned property yourself
is `VO1022`.

A **nullable** scalar (`[Scalar<string>(Nullable = true)]`, or a `T?` value type) round-trips JSON `null`, so its
schema must be null-tolerant. Use ZodSharp's shipped `[NullOrNonWhiteSpace]`, which accepts `null` but rejects an
empty or whitespace-only value, where `[RequiredZod]` (which always rejects `null`) cannot express the check.

> **An escape hatch, not the pipeline.** A `[ZodSchema]` value object's generated `Create` throws `ZodException`
> on failure, and `TryCreate` catches it and returns `false`. That is the right shape for a call site that only
> needs a boolean — but expected rejections belong in the result pipeline, where the errors stay structured and
> the HTTP layer can render them.

## The rejection as a result failure

The case that carries the errors implements `IValidationErrorCarrier`, so the HTTP layer can render it without
knowing the error type:

```csharp
[GenerateResult]
readonly union TenantError(TenantInputInvalid, TenantAlreadyExists);

readonly record struct TenantInputInvalid(Guid TenantId, ImmutableArray<ValidationError> Errors)
    : IValidationErrorCarrier
{
    ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

readonly record struct TenantAlreadyExists(TenantId TenantId);
```

The service hydrates the value object without running its rule, validates it through the generated schema, and
turns a rejection into the carrier case with the generated `AsFailure<TValue>()` helper:

```csharp
public Result<Tenant, TenantError> Register(Guid tenantId, string name)
{
    var candidate = TenantId.Hydrate(tenantId);
    var validation = TenantIdSchema.Validate(candidate);

    if (!validation.IsSuccess)
        return new TenantInputInvalid(tenantId, validation.Errors).AsFailure<Tenant>();

    var id = validation.Value;

    if (_tenants.ContainsKey(id))
        return new TenantAlreadyExists(id).AsFailure<Tenant>();

    Tenant tenant = new(id, name, Enabled: true);
    _tenants[id] = tenant;

    return Result<Tenant, TenantError>.Success(tenant);
}
```

`ToResult<TValue, TError>(errors => ...)` from `Purview.Results.ZodSharp` is the one-call alternative when the
validated value flows straight on; see [ZodSharp Integration](ZodSharp-Integration.md).

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
runs the whole composition: an automatic `[Scalar<Guid>]` tenant identifier whose type-level `[ZodRule]` owns
`Code = "invalid_tenant_id"` and `Origin = "value_object"`, validated into a `Result<Tenant, TenantError>`
failure. It prints each error's code, origin and (empty) path — the identity a host's `MapCode`/`MapOrigin` rules
would key on.

```bash
dotnet run --project src/src/Examples.ValueObjects.Zod
```

## Related

- [ZodSharp Integration](ZodSharp-Integration.md) — producing results that carry validation errors.
- [ZodSharp Problem Details](ZodSharp-ProblemDetails.md) — the per-code, per-category and per-origin rules.
- [Union Errors](Union-Errors.md) — the error union the rejected input becomes a case of.