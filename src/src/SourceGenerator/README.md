# Purview.Results source generator

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.svg)](https://www.nuget.org/packages/Purview.Results)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

An incremental Roslyn source generator that makes C# 15 **union** error cases ergonomic to use with
`Purview.Results.Result<TValue, TError>` and its value-less counterpart `Purview.Results.Result<TError>`. It ships
inside the [`Purview.Results`](https://www.nuget.org/packages/Purview.Results) package; it is not published as a
separate package.

## Installation

```bash
dotnet add package Purview.Results
```

`Purview.Results` provides both the `Result<TValue, TError>` type and this generator: the package carries the
merged Roslyn component under `analyzers/dotnet/cs`, so it is applied to the compilation automatically. There is
no separate generator package to reference.

**Requirements:** a C# 15 compiler with union declaration support (the .NET 11 SDK or later) and
`LangVersion=preview`.

```csharp
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

public readonly record struct TenantNotFound(TenantId TenantId);
public readonly record struct TenantDisabled(TenantId TenantId);
public readonly record struct TenantAlreadyExists(TenantId TenantId);
```

For a union opted in with `[GenerateResult]` the generator emits one strongly typed extension class in the
union's namespace, with two helpers per case — a value-producing `AsFailure<TValue>()` and a non-generic
`AsFailure()` that produces a unit result:

```csharp
namespace Test.Tenancy;

public static class TenantErrorResultExtensions
{
	public static Result<TValue, TenantError> AsFailure<TValue>(this TenantNotFound error) =>
		Result<TValue, TenantError>.Failure(error);

	public static Result<TenantError> AsFailure(this TenantNotFound error) =>
		Result<TenantError>.Failure(error);

	// ... one pair per case: TenantDisabled, TenantAlreadyExists
}
```

Usage:

```csharp
Result<Tenant, TenantError> result = new TenantNotFound(tenantId).AsFailure<Tenant>();
Result<TenantError> unitResult = new TenantNotFound(tenantId).AsFailure();
```

## Why the generator exists

C# composes a union *case* into its union and `Result<TValue, TError>` composes the *union* into a result,
but C# never composes two conversions at once, so the following does **not** compile (`CS0029`):

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => new TenantNotFound(tenantId);
```

The compiler experiments that prove this (and that the explicit forms the helper replaces *do* compile) live
in `src/tests/SourceGenerator.UnitTests/UnionCompilerBehaviourTests.cs`. The same file records that an
extension method declared on the *union* type cannot help either (`CS1929`): C# does not apply union
conversions to an extension-method receiver, which is why one helper is generated **per case type**.

The generator does not change, wrap or replace `Result<TValue, TError>` or `Result<TError>`; it only produces
the missing call site ergonomics. There is no reflection, no `dynamic`, no runtime type discovery and no mutable
static state: every helper is a pure static method that calls the existing `Result<...>.Failure` factory.

## Why there are no implicit conversions

The shortest possible call site would be `return new TenantNotFound(id);` — the generator declaring an implicit
conversion instead of a helper method. C# forbids every route to that, and each rule is recorded as a compiler
experiment in `src/tests/SourceGenerator.UnitTests/UnionCompilerBehaviourTests.cs`:

| Rule | Experiment | Diagnostic |
| --- | --- | --- |
| A user-defined operator cannot be declared in a **static class** — and the generated helper class is `public static class {Union}ResultExtensions` | `ConversionOperator_DeclaredInStaticGeneratedHelperClass_DoesNotCompile` | `CS0715` |
| A conversion operator must be declared by the **source or the target type**; a helper class is neither | `ConversionOperator_DeclaredOutsideSourceOrTargetType_DoesNotCompile` | `CS0556` |
| Conversion operators are **not permitted as extension members** | `ConversionOperator_DeclaredInExtensionBlock_DoesNotCompile` | `CS9282` |
| A conversion operator **cannot declare type parameters of its own**, so `TValue` is out of scope for a non-generic case type | `ConversionOperator_OnNonGenericCaseType_CannotNameTheResultValueType` | `CS0246` |
| **Only one user-defined conversion** may participate in a conversion sequence, so `case → union → Result<…>` can never compose | `Result_GivenCaseValue_DirectAssignmentDoesNotCompile` | `CS0029` |

The only shape the language accepts is a *generic case type* carrying the value type parameter
(`ConversionOperator_OnGenericCaseType_Compiles`) — precisely the shape the generator rejects as `RSG1002`.
One helper per case type is therefore the best ergonomics the language allows, and the package's code fix
offers the rewrite in the IDE when a case value is returned where a result is expected.

A helper-free form does exist, because a cast closes the case → union conversion so that only the library's
union → result conversion remains:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) => (TenantError)new TenantNotFound(tenantId);
```

`Result_GivenUnionCastedCase_Compiles` proves it compiles. It is not prettier than `AsFailure<Tenant>()`, but it
needs no generated code.

## Activation and design

- **Opt-in only.** Nothing is generated for a compilation that never applies `[GenerateResult]`; discovery runs
  through `ForAttributeWithMetadataName` and only the attribute source is produced for every compilation.
- **The attribute is generated** during post-initialization (`GenerateResultAttribute.g.cs`), so consumers do
  not declare it and do not need another package reference.
- **One analyzer, one generator, one diagnostics library.** `ResultsDiagnosticAnalyzer` raises the per-target
  rules and the generator shares the same analysis to decide whether generation can continue (see
  [Diagnostics](#diagnostics)).
- **One suppressor, with one narrow job.** `UnionEqualityDiagnosticSuppressor` answers `CA1815` for opted-in
  unions (see [Diagnostic suppressions](#diagnostic-suppressions)). It reports no diagnostics of its own, so it
  cannot hide a rule.
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

## Diagnostic suppressions

A union declaration gets no value equality from the compiler, so every union raises `CA1815` ("Override equals
and operator equals on value types") unless it is silenced by hand with a `#pragma warning disable CA1815` or a
`[SuppressMessage]`. A `[GenerateResult]` union is the error type of a result and is read by matching its case
type, not by comparing two unions by value, so the package answers that warning for you:

| Suppression ID | Suppressed rule | Applies to |
| --- | --- | --- |
| `RSG2000` | `CA1815` | A declaration that is a union **and** is opted in with `[GenerateResult]` |

The suppression is narrow on purpose:

- A union without `[GenerateResult]` keeps the warning — this package only speaks for the unions it generates
  for.
- The union's **case types** keep the warning, as does every other value type. A case declared as a plain
  `struct` still gets `CA1815`; a `record struct` case never gets it, because records synthesise equality.
- Nothing is hidden: a suppressor can only suppress non-error, configurable diagnostics, and `RSG2000` is a
  suppression id rather than a rule, so `RSG1000`–`RSG1007` remain the only diagnostics this package reports.

Every suppression is logged as an `Info` diagnostic against `RSG2000`, in the verbose build log and in an
MSBuild binlog (and as a suppressed diagnostic in an `/errorlog` SARIF file), so a build can always be audited
for what it suppressed.

**Keeping `CA1815`.** Add `RSG2000` to the compiler's warning suppressions, for example
`<NoWarn>$(NoWarn);RSG2000</NoWarn>` in the project file (or `/nowarn:RSG2000` on a compiler invocation). The
warning then returns for every union declaration, including the opted-in ones.

```xml
<PropertyGroup>
	<NoWarn>$(NoWarn);RSG2000</NoWarn>
</PropertyGroup>
```

Declaring equality on the union is the other way to keep `CA1815` satisfied — it is the code the warning asks
for, and a union that has it never raises the warning in the first place, so the suppressor has nothing to do.

## Build properties

| Property | Default | Purpose |
| --- | --- | --- |
| `DisableResultsSourceGenerator` | `false` | Disables generation while still emitting the opt-in attribute. |

The property is declared as a compiler-visible MSBuild property and shipped to `PackageReference` consumers as
`buildTransitive/Purview.Results.props` (from `Sdk/buildTransitive/Purview.Results.props` in this repository),
so `-p:DisableResultsSourceGenerator=true` — or a `Directory.Build.props` setting — disables the helpers for
consumers too.

## Consumer requirements

- A C# 15 compiler with union declaration support (`.NET 11` SDK or later) and `LangVersion=preview`.
- A reference to `Purview.Results`, which provides both `Result<TValue, TError>` and the unit `Result<TError>`,
  and this generator.

## Call-site ergonomics: the code fix

Because the generator cannot make a case value convert (`AsFailure<TValue>()` and `AsFailure()` are the best the
language allows), the package also ships an IDE code fix: `UnionCaseResultCodeFixProvider` in the companion
`SourceGenerator.CodeFixes` component (`src/src/SourceGenerator.CodeFixes`).

It answers the compiler's `CS0029` ("cannot implicitly convert") and rewrites a returned case value into the
generated helper, so `return new TenantNotFound(id);` becomes
`return new TenantNotFound(id).AsFailure<Tenant>();` from the lightbulb, or `...AsFailure();` when the method
returns a unit `Result<TenantError>`.

A fix is only offered when the rewrite will bind:

| Guard | Why |
| --- | --- |
| The converted type is `Purview.Results.Result<TValue, TError>` or the unit `Purview.Results.Result<TError>` | Only results have generated helpers; a value result gets `AsFailure<TValue>()` and a unit result the non-generic `AsFailure()` |
| `TError` is a union **and** opted in with `[GenerateResult]` | The helper is only generated for opted-in unions |
| The expression's type is one of `TError`'s case types | The helper exists once per case |
| The union is reachable by its simple name at the call site | The helper class is generated into the union's own namespace |

The component is not packable on its own: it needs `Microsoft.CodeAnalysis.CSharp.Workspaces`, which only the
IDE host provides, so `Purview.Results` packs it into `analyzers/dotnet/cs/` as a second analyzer assembly
(never IL-merged into the generator). The two projects stay independent — the code fix does not reference the
generator — so no reference cycle can form and Workspaces never enters the generator's dependencies.

## Tests

`src/tests/SourceGenerator.UnitTests` contains the source-generation tests (basic generation,
multiple unions, namespaces, accessibility, case kinds, diagnostics, incremental caching and determinism),
the compiler experiments above, and runtime/integration tests that execute the generated helpers against the
real `Result<TValue, TError>` type.

The suppressor is covered by `UnionEqualityDiagnosticSuppressorTests`. The real `CA1815` comes from the .NET
analyzers the SDK loads, which a unit-test compilation cannot reference, so those tests report the same id at the
same location from a test-only analyzer (`Ca1815ReporterAnalyzer`) and run it beside the shipped suppressor
through `CompilationWithAnalyzers`. The compilation under test is still a real one: it is produced by running the
generator, so the union, the generated attribute and the generated helpers are all present.

The code-fix tests live in `UnionCaseResultCodeFixProviderTests` and use `UnionCodeFixTestHarness`: the
framework's code-fix test base is driven by an analyzer's diagnostics, and this fix answers a compiler
diagnostic, so the harness builds the compilation itself, runs the generator (the generated attribute and
helpers must exist for the rewrite to bind), applies the fix through an `AdhocWorkspace`, and recompiles the
rewritten source to prove it compiles.

## Examples

[`src/src/Examples.Basic`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Basic)
declares a `[GenerateResult]` union over the Tenant* domain the README uses and calls the helpers this generator
emits for each of its cases:

```csharp
Result<Tenant, TenantError> result = new TenantNotFound(tenantId).AsFailure<Tenant>();
Result<TenantError> unitResult = new TenantNotFound(tenantId).AsFailure();
```

The [repository README](https://github.com/purview-dev/results#examples) covers the ZodSharp, ASP.NET Core and
ASP.NET Core + Zod examples as well.

## Agent skills

This package ships the `purview-results-union-errors` agent skill, the `purview-results-union-author` agent and
the `migrate-error-returns-to-result-unions` prompt under `.agents/`, so a repository that imports
`Purview.BuildSdk` receives them in its own `.agents/` folder on the next restore or build. The skill covers
union modelling, the generated helpers, the diagnostics table, and the language rules that make the helper
necessary.

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
