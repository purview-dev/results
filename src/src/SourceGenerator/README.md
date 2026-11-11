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

**Requirements:** a C# 15 compiler with union declaration support (the .NET 11 SDK or later) and a `net11.0`
target. C# 15 is the default language version for `net11.0`, so no `LangVersion` is needed.

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

The same class also declares a C# 14 extension block on the union type itself, so a case can be converted by
naming the union (`TenantError.Failure(case)`) rather than the case:

```csharp
public static class TenantErrorResultExtensions
{
	// ... the per-case AsFailure helpers above ...

	extension(TenantError)
	{
		public static Result<TenantError> Failure(TenantNotFound error) =>
			Result<TenantError>.Failure(error);

		public static Result<TValue, TenantError> Failure<TValue>(TenantNotFound error) =>
			Result<TValue, TenantError>.Failure(error);

		// ... one Failure pair per case: TenantDisabled, TenantAlreadyExists ...

		public static Result<TenantError> Success() =>
			Result<TenantError>.Success();

		public static Result<TValue, TenantError> Success<TValue>(TValue value) =>
			Result<TValue, TenantError>.Success(value);
	}
}
```

Usage:

```csharp
Result<Tenant, TenantError> result = TenantError.Failure<Tenant>(new TenantNotFound(tenantId));
Result<TenantError> unitResult = TenantError.Failure(new TenantNotFound(tenantId));
Result<TenantError> success = TenantError.Success();
Result<Tenant, TenantError> valueSuccess = TenantError.Success(tenant);
```

The union-receiver factory is the **shared-case safe** form. When a case type is a case of more than one union, the
per-case `AsFailure` helper is generated once — for the union that sorts first — and reported as `RSG1006`, so a
case-receiver call can bind to the wrong union. The factory names the union by its receiver, so it is generated for
every union and covers every one of the union's cases, including a shared one.

## Union inclusion

A case type that is itself a union is an **included union**: the including union's factory also covers every case
reachable through it, so a case of an included union can be converted by naming the including union. The value is
constructed nested, innermost first:

```csharp
[GenerateResult]
public readonly union BillingError(BillingAccountMissing, BillingServiceUnavailable);

[GenerateResult]
public readonly union RegisterTenantError(TenantError, BillingError);

// BillingAccountMissing is reachable through BillingError, so the factory covers it and builds the nested value.
Result<Tenant, RegisterTenantError> result =
	RegisterTenantError.Failure<Tenant>(new BillingAccountMissing(accountId));
// equivalent to:
Result<Tenant, RegisterTenantError> explicitNesting =
	RegisterTenantError.Failure<Tenant>(new BillingError(new BillingAccountMissing(accountId)));
```

Inclusion is automatic for every union-typed case; no attribute is needed. It is also the way to **avoid `RSG1006`
entirely**: list the callee's union as a case instead of repeating its leaf cases. A union-typed case shared by two
unions is the intended composition pattern, so it is **not** reported as `RSG1006` and its ambiguous per-case
`AsFailure` helper is not generated for any union; the union-receiver factory is the only (and safe) form. A case
reachable through two different included unions cannot name a construction path, so it is reported as `RSG1008` and
its inclusion factory is skipped.

## Custom unions and non-boxing storage

`[GenerateResult]` is not limited to a `union` declaration. Any class or struct with the `[Union]` attribute and
the language's basic union pattern is a union, so a custom type with typed storage can keep a value-type case
unboxed — the *non-boxing access pattern* adds a `HasValue` property and one `TryGetValue` overload per case:

```csharp
[Union]
public readonly struct CountOrLabel : IUnion
{
    private readonly Count _count;
    private readonly Label? _label;
    private readonly byte _tag;

    public CountOrLabel(Count value) { _count = value; _label = null; _tag = 1; }
    public CountOrLabel(Label value) { _count = default; _label = value; _tag = 2; }

    public object? Value => _tag switch { 1 => _count, 2 => _label, _ => null };
    public bool HasValue => _tag != 0;

    public bool TryGetValue(out Count value) { value = _count; return _tag == 1; }
    public bool TryGetValue(out Label value) { value = _label.GetValueOrDefault(); return _tag == 2; }
}
```

The generator treats it like any other union and emits the same per-case `AsFailure` helpers and
union-receiver factory. Those helpers are plain `Result<...>.Failure(...)` calls, and `Result<TValue, TError>`
stores its error in a typed field, so nothing in the generated path boxes the case either. A compiler-generated
`union` declaration always stores its contents as `object?`; reach for a custom union when an error type is on a
hot path. `NonBoxingUnionTests` proves the match allocates nothing.

## Generic and member-provider unions

A generic union's own type parameters and constraints are repeated on the generated helpers and on a generic
extension block that carries the union-receiver factory:

```csharp
public readonly record struct None;
public readonly record struct Some<T>(T Value);

[GenerateResult]
public readonly union Maybe<T>(None, Some<T>);

// Static extension-member type arguments are the extension block's parameters first, then the method's.
Result<int, Maybe<string>> failure = Maybe<string>.Failure<string, int>(new Some<string>("present"));
Result<int, Maybe<string>> helper = new Some<string>("present").AsFailure<string, int>();
```

A custom union may place its creation members on a nested `IUnionMembers` provider. The generator reads the case
list from the provider's public static single-parameter `Create` methods and constructs values through
`IUnionMembers.Create`, so a union with a private constructor works:

```csharp
[Union]
[GenerateResult]
public readonly struct ProvidedError : IUnion, ProvidedError.IUnionMembers
{
    private readonly object? _value;
    private ProvidedError(object? value) => _value = value;

    public interface IUnionMembers
    {
        static ProvidedError Create(ProvidedCase value) => new(value);
        object? Value { get; }
    }

    object? IUnionMembers.Value => _value;
    public object? Value => _value;
}
```

A provider that declares no usable `Create` method is reported as `RSG1007` and blocks generation. A case that
names a type parameter the union does not declare is reported as `RSG1002` and skipped. A bare union type
parameter case is supported by the union-receiver factory (but not by a per-case `AsFailure` helper); a non-named
case such as an array is reported as `RSG1009`.

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
(`ConversionOperator_OnGenericCaseType_Compiles`) — the shape a generic union's helper declares instead of a
generated conversion.
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

- **`ResultsDiagnosticAnalyzer` reports the per-target rules** (`RSG1000`–`RSG1004`, `RSG1007`–`RSG1009`) in the
  IDE and in build output.
- **The generator reports the compilation-wide rules** (`RSG1005`, `RSG1006`) that need every opted-in union
  in the compilation, and `RSG9000` when the emitter fails unexpectedly.
- **The generator never reports a rule the analyzer reports.** It still runs the same shared analysis, so the
  `IsBlocking` decision (`DiagnosticLibrary.IsBlocking`, the single blocking policy) comes from the same
  findings — the rule is simply never surfaced twice.

| ID | Severity | Reported by | Blocking | Description |
| --- | --- | --- | --- | --- |
| `RSG1000` | Error | Analyzer | Yes | `[GenerateResult]` was applied to a type that is not a union. |
| `RSG1001` | Error | Analyzer | Yes | The union declares no union cases. |
| `RSG1002` | Error | Analyzer | No (only the offending case is skipped) | A case type names a type parameter the union does not declare, or the union is nested in a generic type. A generic union itself is supported. |
| `RSG1003` | Error | Analyzer | Union: yes / case: no | A type that generated code must reference is not accessible (for example a `file` type). |
| `RSG1004` | Error | Analyzer | No | The same union is configured more than once (for example on two partial declarations); the helpers are generated once. |
| `RSG1005` | Error | Generator | Yes (the colliding union is skipped) | Two unions produce the same generated class name in one namespace. |
| `RSG1006` | Warning | Generator | No (only the shared leaf case's `AsFailure` helper is skipped) | A **leaf** case type is shared with another union, so its per-case helper is generated once; the union-receiver `Failure(...)` factory is generated for every union and is the shared-case safe form. A union-typed case shared across unions is composition, not a shared leaf, and is not reported. |
| `RSG1007` | Error | Analyzer | Yes | The union's `IUnionMembers` provider declares no public static single-parameter `Create` method, so it does not declare any cases. |
| `RSG1008` | Warning | Analyzer | No (only the ambiguous case's inclusion factory is skipped) | A case is reachable through two different included unions, so no inclusion factory can name its construction path. List the case through the union that directly declares it instead. |
| `RSG1009` | Warning | Analyzer | No (only the unsupported case is skipped) | A case type is not a named type (for example an array), so no helper or factory can be declared against it. Wrap it in a record or struct case instead. |
| `RSG9000` | Error | Generator | — | The generator failed unexpectedly. The failure is reported instead of escaping, because an escaping exception surfaces as `CS8785` and discards *all* generated output for the compilation. Please report it. |

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
  suppression id rather than a rule, so `RSG1000`–`RSG1009` and `RSG9000` remain the only diagnostics this
  package reports.

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

- A C# 15 compiler with union declaration support (`.NET 11` SDK or later) and a `net11.0` target. C# 15 is the
  default language version for `net11.0`, so no `LangVersion` is needed.
- A reference to `Purview.Results`, which provides both `Result<TValue, TError>` and the unit `Result<TError>`,
  and this generator.

## Call-site ergonomics: the code fix

Because the generator cannot make a case value convert (`Union.Failure(...)` and the per-case `AsFailure<TValue>()`
are the best the language allows), the package also ships an IDE code fix: `UnionCaseResultCodeFixProvider` in the
companion `SourceGenerator.CodeFixes` component (`src/src/SourceGenerator.CodeFixes`).

It answers the compiler's `CS0029` ("cannot implicitly convert") and rewrites a returned case value into the
union-receiver factory, so `return new TenantNotFound(id);` becomes
`return TenantError.Failure<Tenant>(new TenantNotFound(id));` from the lightbulb, or
`TenantError.Failure(new TenantNotFound(id))` when the method returns a unit `Result<TenantError>`. A generic
union repeats its type arguments first: `Maybe<string>.Failure<string, Tenant>(new Some<string>("x"))`. Naming the
union keeps the rewrite correct even when the case type is shared with another union (`RSG1006`).

A fix is only offered when the rewrite will bind:

| Guard | Why |
| --- | --- |
| The converted type is `Purview.Results.Result<TValue, TError>` or the unit `Purview.Results.Result<TError>` | Only results have generated factories; a value result gets `Failure<TValue>(case)` (or `Failure<T, TValue>(case)` for a generic union) and a unit result `Failure(case)` |
| `TError` is a union **and** opted in with `[GenerateResult]` | The factory is only generated for opted-in unions |
| The expression's type is one of `TError`'s case types | The factory exists once per case |
| The union is reachable by its simple name at the call site | The factory extension block is generated into the union's own namespace |

The component is not packable on its own: it needs `Microsoft.CodeAnalysis.CSharp.Workspaces`, which only the
IDE host provides, so `Purview.Results` packs it into `analyzers/dotnet/cs/` as a second analyzer assembly
(never IL-merged into the generator). The code fix does not reference the generator; the generator references the
code-fix project analyzer-style only (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`), so it is
built alongside without becoming a compile-time dependency. No reference cycle can form and Workspaces never
enters the generator's dependencies.

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

The `purview-results-union-errors` agent skill covers union modelling, the generated helpers, the diagnostics table, and the language rules that make the helper necessary. No package ships skills: install them, with every other Purview.Results skill, from the git repository with the skills CLI:

```bash
npx skills add purview-dev/results
```

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
