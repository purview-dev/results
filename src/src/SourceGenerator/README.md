# Purview.Results.SourceGenerator

An incremental Roslyn source generator that makes C# 15 **union** error cases ergonomic to use with
`Purview.Results.Result<TValue, TError>`.

```csharp
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

public readonly record struct TenantNotFound(TenantId TenantId);
public readonly record struct TenantDisabled(TenantId TenantId);
public readonly record struct TenantAlreadyExists(TenantId TenantId);
```

For a union opted in with `[GenerateResult]` the generator emits one strongly typed extension class in the
union's namespace:

```csharp
namespace Test.Tenancy;

public static class TenantErrorResultExtensions
{
	public static Result<TValue, TenantError> AsFailure<TValue>(this TenantNotFound error) =>
		Result<TValue, TenantError>.Failure(error);

	public static Result<TValue, TenantError> AsFailure<TValue>(this TenantDisabled error) =>
		Result<TValue, TenantError>.Failure(error);

	public static Result<TValue, TenantError> AsFailure<TValue>(this TenantAlreadyExists error) =>
		Result<TValue, TenantError>.Failure(error);
}
```

Usage:

```csharp
Result<Tenant, TenantError> result = new TenantNotFound(tenantId).AsFailure<Tenant>();
```

## Why the generator exists

C# composes a union *case* into its union and `Result<TValue, TError>` composes the *union* into a result,
but C# never composes two conversions at once, so the following does **not** compile (`CS0029`):

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => new TenantNotFound(tenantId);
```

The compiler experiments that prove this (and that the explicit forms the helper replaces *do* compile) live
in `src/tests/Results.SourceGenerator.UnitTests/UnionCompilerBehaviourTests.cs`. The same file records that an
extension method declared on the *union* type cannot help either (`CS1929`): C# does not apply union
conversions to an extension-method receiver, which is why one helper is generated **per case type**.

The generator does not change, wrap or replace `Result<TValue, TError>`; it only produces the missing call
site ergonomics. There is no reflection, no `dynamic`, no runtime type discovery and no mutable static state:
every helper is a pure static method that calls the existing `Result<TValue, TError>.Failure` factory.

## Activation and design

- **Opt-in only.** Nothing is generated for a compilation that never applies `[GenerateResult]`; discovery runs
  through `ForAttributeWithMetadataName` and only the attribute source is produced for every compilation.
- **The attribute is generated** during post-initialization (`GenerateResultAttribute.g.cs`), so consumers do
  not declare it and do not need another package reference.
- **One analyzer, one generator, one diagnostics library.** `ResultsDiagnosticAnalyzer` raises the per-target
  rules and the generator shares the same analysis to decide whether generation can continue (see
  [Diagnostics](#diagnostics)).
- **Union membership is decided by the language** (`ITypeSymbol.IsUnion`), never by type names, source text,
  reflection or the union's runtime value.
- **Case types come from the language's own rule**: a union's public single-parameter constructors define its
  case types, and a `union` declaration synthesises one per declared case.
- **Immutable pipeline models.** The pipeline resolves Roslyn symbols during discovery and converts them into
  value-equatable models (`TypeIdentity`, `TypeReference`, `EquatableArray<T>`, spans/paths as values). No
  syntax nodes, symbols, semantic models or compilations are retained in cached state, so unrelated edits do
  not regenerate output — proven stage-by-stage by `ResultsSourceGeneratorCacheTests`.
- **Deterministic output.** Cases are ordered by fully-qualified name, unions are processed in ordinal order,
  hint names are derived from the union's identity, and no timestamps, GUIDs or machine paths are emitted.
- **Accessibility never widens**: the generated class is `public` only when the union and every case type it
  references are public, and `internal` otherwise.
- **Nullable-aware**: generated files start with `#nullable enable`, reference fully qualified names, and do
  not add `?` to non-nullable case types.

## Diagnostics

The diagnostics live in one shared library (`Diagnostics/DiagnosticLibrary.cs` +
`Diagnostics/ResultUnionDiagnostics.cs` + `Diagnostics/ResultDiagnostic.cs`) that both hosts consume:

- **`ResultsDiagnosticAnalyzer` reports the per-target rules** (`RSG1000`–`RSG1004`, `RSG1007`) in the IDE
  and in build output.
- **The generator reports the compilation-wide rules** (`RSG1005`, `RSG1006`) that need every opted-in union
  in the compilation.
- **The generator never reports a rule the analyzer reports.** It still runs the same shared analysis, so the
  `IsBlocking` decision (`DiagnosticLibrary.IsBlocking`, the single blocking policy) comes from the same
  findings — the rule is simply never surfaced twice.

| ID | Severity | Reported by | Blocking | Description |
| --- | --- | --- | --- | --- |
| `RSG1000` | Error | Analyzer | Yes | `[GenerateResult]` was applied to a type that is not a union. |
| `RSG1001` | Error | Analyzer | Yes | The union declares no union cases. |
| `RSG1002` | Error | Analyzer | Union: yes / case: no | Generic union, or a case type that contains type parameters. |
| `RSG1003` | Error | Analyzer | Union: yes / case: no | A type that generated code must reference is not accessible (for example a `file` type). |
| `RSG1004` | Error | Analyzer | No | The same union is configured more than once (for example on two partial declarations); the helpers are generated once. |
| `RSG1005` | Error | Generator | Yes (the colliding union is skipped) | Two unions produce the same generated class name in one namespace. |
| `RSG1006` | Warning | Generator | No (only the shared case's helper is skipped) | A case type is shared with another union, so its helper is generated once to keep call sites unambiguous. |
| `RSG1007` | Error | Analyzer | Yes | The union uses an `IUnionMembers` member provider, which the first implementation does not support. |

Case-level findings never block the union: the remaining cases are still generated, and the skipped case is
reported.

## Build properties

| Property | Default | Purpose |
| --- | --- | --- |
| `ResultsSourceGenerator_Disable` | `false` | Disables generation while still emitting the opt-in attribute. |

## Consumer requirements

- A C# 15 compiler with union declaration support (`.NET 11` SDK or later) and `LangVersion=preview`.
- A reference to `Purview.Results` for `Result<TValue, TError>`.

## Tests

`src/tests/Results.SourceGenerator.UnitTests` contains the source-generation tests (basic generation,
multiple unions, namespaces, accessibility, case kinds, diagnostics, incremental caching and determinism),
the compiler experiments above, and runtime/integration tests that execute the generated helpers against the
real `Result<TValue, TError>` type.
