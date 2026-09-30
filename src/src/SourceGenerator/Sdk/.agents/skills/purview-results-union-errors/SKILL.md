---
name: purview-results-union-errors
description: "Use when modelling expected failures as a C# 15 union and converting its cases into Purview.Results values — declaring [GenerateResult] unions, using the generated AsFailure<TValue>() helpers, reacting to the RSG1000-RSG1007 diagnostics, or explaining why a union case cannot convert implicitly."
---

# Modelling error unions for Purview.Results

Use this skill when a codebase models expected failures as a **union** and returns
`Result<TValue, TUnion>` from the `Purview.Results` package.

## The shape

```csharp
using Purview.Results;
using Purview.Results.SourceGeneration;

// One small, focused type per case.
public readonly record struct TenantNotFound(TenantId TenantId);
public readonly record struct TenantDisabled(TenantId TenantId);
public readonly record struct TenantAlreadyExists(TenantId TenantId);

// One union per operation family.
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

public static Result<Tenant, TenantError> Get(TenantId id) =>
    _tenants.TryGet(id, out var tenant)
        ? Result<Tenant, TenantError>.Success(tenant)
        : new TenantNotFound(id).AsFailure<Tenant>();   // generated
```

`AsFailure<TValue>()` is generated **once per case type**, into `public static class {Union}ResultExtensions` in
the union's own namespace, and it calls `Result<TValue, TUnion>.Failure(case)`.

## Why the helper is required

The generator cannot declare an implicit conversion from a case to the result, and neither can you. Five
independent C# rules block it:

| Attempt | Diagnostic |
| --- | --- |
| Declare the operator beside the helpers | `CS0715` — operators are illegal in a **static class** |
| Declare it in any other helper class | `CS0556` — a conversion must be declared by the **source or target type** |
| Declare it as an extension member | `CS9282` — **conversions are not allowed in extension blocks** |
| Make it generic over the result's value type | `CS0246` — operators **cannot declare type parameters** |
| Rely on `case → union → result` composing | `CS0029` — **one user-defined conversion per sequence** |

The only shape the language accepts is a *generic case type* carrying the value type parameter, which the
generator rejects as `RSG1002` — so `AsFailure<TValue>()` is the ergonomics the language allows.

A helper-free form exists when a cast reads better, because the cast closes the first conversion:

```csharp
Result<Tenant, TenantError> Get(TenantId id) => (TenantError)new TenantNotFound(id);
```

`Purview.Results.SourceGenerator` also ships an IDE code fix: when you write
`return new TenantNotFound(id);` in a `Result<TValue, TUnion>`-returning member, the lightbulb offers the
`AsFailure<TValue>()` rewrite. The fix is only offered when the rewrite will bind (the converted type is a
result, the error type is a `[GenerateResult]` union, the value is one of its cases, and the union is in scope
by simple name).

## Rules of thumb

- **One case type, one meaning.** Cases are the vocabulary of the failure surface; a case with a `string
  Reason` invites callers to match on that string instead of on the type.
- **One union per operation family**, not one per method: `TenantError` covers every way tenant work fails, so
  the union stays a stable contract while methods come and go.
- **Never re-derive the case from the union in the caller** — keep the case strongly typed and let the HTTP or
  UI layer map it (see the `purview-results-http-mapping` skill).
- **Small unions are better.** A union with a dozen cases is usually several operation families sharing one
  name.
- **Opt in deliberately.** `[GenerateResult]` on a union you do not return from a result produces dead code;
  apply it where the union crosses a `Result<TValue, TUnion>` boundary.

## Diagnostics

The package ships an analyzer and a generator that share one diagnostics library.

| ID | Severity | Meaning |
| --- | --- | --- |
| `RSG1000` | Error | `[GenerateResult]` was applied to a type that is not a union |
| `RSG1001` | Error | The union declares no union cases |
| `RSG1002` | Error | Generic union, or a case type that contains type parameters |
| `RSG1003` | Error | A type generated code must reference is not accessible (for example a `file` type) |
| `RSG1004` | Error | The same union is configured more than once; the helpers are generated once |
| `RSG1005` | Error | Two unions produce the same generated class name in one namespace |
| `RSG1006` | Warning | A case type is shared with another union, so its helper is generated once |
| `RSG1007` | Error | The union uses an `IUnionMembers` member provider, which is not supported |

`RSG1004` and `RSG1006` are non-blocking: the union (or the shared case) is skipped and reported.

Unsupported by design: `IUnionMembers` member providers (`RSG1007`) and generic unions (`RSG1002`).

## Requirements and switches

- **.NET 11 SDK or later** with `LangVersion=preview` — union declarations are a preview language feature.
- A reference to `Purview.Results` for the result type the helpers build.
- `-p:ResultsSourceGenerator_Disable=true` (or a `Directory.Build.props` setting) disables the helpers while
  the opt-in attribute is still emitted, which is useful when the generated code has to be inspected in
  isolation.

## Checklist

1. Cases are small records with meaningful payloads.
2. The union is a `union` declaration annotated with `[GenerateResult]`.
3. Every method that fails for an expected reason returns `Result<TValue, TUnion>`.
4. Failures are produced with `new Case(...).AsFailure<TValue>()` (or the cast form).
5. The build is clean of `RSG1000`–`RSG1007`.
6. The union is mapped to responses once, in the host (see `purview-results-http-mapping`).
