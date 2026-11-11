---
name: purview-results-union-errors
description: "Use when modelling expected failures as a C# 15 union and converting its cases into Purview.Results values — declaring [GenerateResult] unions, using the generated per-case AsFailure<TValue>()/AsFailure() helpers and the union-receiver Union.Failure(...)/Union.Success(...) factories, reacting to the RSG1000-RSG1009 and RSG9000 diagnostics, or explaining why a union case cannot convert implicitly."
---

# Modelling error unions for Purview.Results

Use this skill when a codebase models expected failures as a **union** and returns
`Result<TValue, TUnion>` or the value-less `Result<TUnion>` from the `Purview.Results` package.

Replacing thrown domain exceptions, `bool`/`out` pairs or error enums in existing code? The
step-by-step playbook is in
[references/migrating-error-returns.md](references/migrating-error-returns.md).

## The shape

```csharp
using Purview.Results;

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

`AsFailure<TValue>()` and the non-generic `AsFailure()` are generated **once per case type**, into
`static class {Union}ResultExtensions` in the union's own namespace (`public` only when the union and every case it
references are visible, otherwise `internal`; a nested union's class is prefixed with its containing types, as
`Outer_TenantErrorResultExtensions`). The generic helper calls
`Result<TValue, TUnion>.Failure(case)`; the non-generic one calls `Result<TUnion>.Failure(case)` and produces a
value-less unit result:

```csharp
Result<Tenant, TenantError> Get(TenantId id) => new TenantNotFound(id).AsFailure<Tenant>();
Result<TenantError> Delete(TenantId id) => new TenantNotFound(id).AsFailure();
```

The same class declares a C# 14 extension block on the union type, so the union can name the call instead of the
case:

```csharp
Result<Tenant, TenantError> Get(TenantId id) => TenantError.Failure<Tenant>(new TenantNotFound(id));
Result<TenantError> Delete(TenantId id) => TenantError.Failure(new TenantNotFound(id));
Result<TenantError> ok = TenantError.Success();
Result<Tenant, TenantError> created = TenantError.Success(tenant);
```

Prefer the union-receiver factory when a case type is shared with another union (`RSG1006`): the per-case
`AsFailure` helper is generated once and can bind to the wrong union, while `Union.Failure(...)` always names the
union you want. A case type that is itself a union is an *included union*: sharing it is composition, so it is not
reported, and the factory also covers the included union's cases by building the nested value.

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

`Purview.Results` also ships an IDE code fix: when you write
`return new TenantNotFound(id);` in a `Result<TValue, TUnion>`- or `Result<TUnion>`-returning member, the
lightbulb offers the `Union.Failure<TValue>(case)` or `Union.Failure(case)` rewrite. The fix names the union, so it
is correct even when the case type is shared with another union. It is only offered when the rewrite will bind (the
converted type is a result, the error type is a `[GenerateResult]` union, the value is one of its cases or a case
reachable through an included union, and the union is in scope by simple name).

## Chaining across services

When one service calls another, the caller's operation-family union takes the callee's **error union** as a case,
then the widening `Bind` (or a `TryGetError` guard) lifts the callee's failure into the caller's contract:

```csharp
[GenerateResult]
public readonly union RegisterTenantError(TenantError, BillingError);

Result<Tenant, RegisterTenantError> Register(TenantId id, string name) =>
    billing.ReserveQuota(id)
        .Bind(quota => CreateTenant(quota, name), error => error);   // BillingError -> RegisterTenantError
```

List the union as a case, not its leaf cases: the caller's union-receiver factory then covers the callee's cases and
builds the nested value (`CallerUnion.Failure(leaf)`), so no leaf is shared and no `RSG1006` is raised. A leaf listed
in two unions is a shared leaf: its per-case helper is generated for only one of them, so a case-receiver call site
can bind to the wrong union; convert it with the union-receiver factory instead. A case reachable through two
different included unions is reported as `RSG1008` and its inclusion factory is skipped. To keep the callee's types
out of the caller's contract, map its cases into the caller's own case types with `MapError` instead — the compiler
then forces the mapping to stay exhaustive as the callee's union grows. The ASP.NET Core mapper resolves a union
error to its innermost case, so a leaf mapping wins and an enclosing-union mapping still applies when the leaf has
none.

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
| `RSG1006` | Warning | A **leaf** case type is shared with another union, so its per-case `AsFailure` helper is generated once; the union-receiver `Failure(...)` factory is generated for every union. A union-typed case shared across unions is composition and is not reported |
| `RSG1007` | Error | The union uses an `IUnionMembers` member provider, which is not supported |
| `RSG1008` | Warning | A case is reachable through two different included unions, so no inclusion factory can name its construction path; that case's inclusion factory is skipped |
| `RSG1009` | Warning | A case type is not a named type (an array, for example), so no helper or factory can be declared against it; wrap it in a record or struct case |
| `RSG9000` | Error | The generator failed unexpectedly; reported instead of letting `CS8785` discard every union's output. Please report it |

Not every finding stops generation. `RSG1004` is generated once; `RSG1006`, `RSG1008` and `RSG1009` skip only the
affected case's helper or factory; `RSG1002` and `RSG1003` skip only the offending case when it is a case type
rather than the union itself. The other rules skip the union.

Unsupported by design: `IUnionMembers` member providers (`RSG1007`) and generic unions (`RSG1002`).

## Trimming, Native AOT and Blazor

The generated helpers and factories are plain static calls to `Result<...>.Failure`/`.Success` and union
constructors: no reflection, no dynamic code. A `[GenerateResult]` union is safe in a trimmed or Native AOT
application and in a Blazor WebAssembly client (trimmed or `RunAOTCompilation=true`).

## CA1815 needs no pragma

A union declaration gets no value equality from the compiler, so it raises `CA1815` unless it is silenced by hand.
`[GenerateResult]` unions do not need that: the package ships a diagnostic suppressor that answers `CA1815` for
every union opted in with the attribute, because a result union is read by matching its case type rather than
compared by value.

- Only the union is covered. A union without `[GenerateResult]`, the union's case types and every other value
  type keep the warning, and a case declared as a plain `struct` should still be a `record struct`.
- The suppression is logged as an `Info` diagnostic under the suppression id `RSG2000`, so a build audit can see
  exactly what was suppressed.
- `RSG2000` is a suppression id, not a rule: it never appears in the diagnostics table above, and
  `<NoWarn>$(NoWarn);RSG2000</NoWarn>` brings `CA1815` back.

## Requirements and switches

- **.NET 11 SDK or later and a `net11.0` target** in the project that declares the union: `IUnion`/`UnionAttribute`
  do not exist before .NET 11. C# 15 is the default language version for `net11.0`, so no `LangVersion` is needed.
  Projects that only *use* the result types can target `net10.0`.
- A reference to `Purview.Results`, which provides both result shapes (`Result<TValue, TError>` and
  `Result<TError>`) and this generator.
- `-p:DisableResultsSourceGenerator=true` (or a `Directory.Build.props` setting) disables the helpers while
  the opt-in attribute is still emitted, which is useful when the generated code has to be inspected in
  isolation.
- **Performance:** a compiler `union` declaration boxes a value-type case. If the error union is on a hot path,
  declare a custom `[Union]` type with typed storage and the non-boxing access pattern (`HasValue` plus
  `TryGetValue` per case); `[GenerateResult]` works on it unchanged, and pattern matching then stays unboxed.

## Checklist

1. Cases are small records with meaningful payloads.
2. The union is a `union` declaration annotated with `[GenerateResult]`.
3. Every method that fails for an expected reason returns `Result<TValue, TUnion>`, or `Result<TUnion>` when the
   success carries no value.
4. Failures are produced with `new Case(...).AsFailure<TValue>()` (or `AsFailure()` for a unit result, the
   union-receiver `Union.Failure<TValue>(case)` / `Union.Failure(case)` factory, or the cast form).
5. The build is clean of `RSG1000`–`RSG1009` and `RSG9000`.
6. The union is mapped to responses once, in the host (see `purview-results-http-mapping`).
