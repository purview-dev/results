# Source Generator

`Purview.Results.SourceGenerator` is an incremental Roslyn source generator that makes C# 15 **union** error cases
ergonomic with `Result<TValue, TError>`. It generates the `[GenerateResult]` attribute, one `AsFailure<TValue>()`
helper per union case, and the diagnostics that keep unsupported shapes out of a build.

## Installation

```bash
dotnet add package Purview.Results.SourceGenerator
dotnet add package Purview.Results
```

The component targets `netstandard2.0` (with the SDK's Roslyn defaults) so any compiler host can load it.
Requirements: a C# 15 compiler with union declaration support (the .NET 11 SDK or later) and
`LangVersion=preview`.

## Activation and design

- **Opt-in only.** Nothing is generated for a compilation that never applies `[GenerateResult]`; discovery runs
  through `ForAttributeWithMetadataName` and only the attribute source is produced for every compilation.
- **The attribute is generated** during post-initialization (`GenerateResultAttribute.g.cs`), so consumers do not
  declare it and do not need another package reference.
- **One analyzer, one generator, one diagnostics library.** The analyzer raises the per-target rules; the
  generator shares the same analysis to decide whether generation can continue. No rule is reported twice.
- **One suppressor, with one narrow job.** `UnionEqualityDiagnosticSuppressor` answers `CA1815` for opted-in
  unions only and reports no diagnostics of its own.
- **Union membership is decided by the language** (`ITypeSymbol.IsUnion`), never by type names, source text,
  reflection or the union's runtime value.
- **Case types come from the language's own rule**: a union's public single-parameter constructors define its
  case types, and a `union` declaration synthesises one per declared case.

## The incremental pipeline

The pipeline resolves Roslyn symbols during discovery and converts them into value-equatable models
(`ResultUnionModel`, `ResultUnionCaseModel`, `ResultSourceLocation`, `EquatableArray<T>`). No `ISymbol`,
`Compilation`, `SemanticModel`, `IOperation`, `SyntaxNode`, `SyntaxTree` or `Location` ever reaches cached
state, which is what stops an unrelated edit from regenerating output.

`ResultsSourceGeneratorCacheTests` proves this stage by stage with `GenerateIncrementalAsync`: `New` on the first
run, `Cached`/`Unchanged` on an identical rerun, and `Modified` only for the stages whose inputs actually changed.
`CodeWriter` is created inside the `RegisterSourceOutput` callback, never stored in cached state.

## Deterministic output

- Cases are ordered **ordinally by fully-qualified name**.
- Unions are processed in **ordinal order**.
- Hint names derive from the union's identity.
- Nothing emits timestamps, GUIDs or machine paths, so the same inputs always produce byte-identical output.

## Accessibility and nullability

Accessibility never widens: the generated class is `public` only when the union and every case type it references
are visible; otherwise it is `internal`. Generated files start with `#nullable enable`, reference fully qualified
names, and do not add `?` to non-nullable case types.

## Generated shape

```csharp
namespace Test.Tenancy;

public static class TenantErrorResultExtensions
{
    public static Result<TValue, TenantError> AsFailure<TValue>(this TenantNotFound error) =>
        Result<TValue, TenantError>.Failure(error);

    public static Result<TValue, TenantError> AsFailure<TValue>(this TenantDisabled error) =>
        Result<TValue, TenantError>.Failure(error);
}
```

The generated helpers are pure static methods that call the existing `Result<TValue, TError>.Failure` factory:
there is no reflection, no `dynamic`, no runtime type discovery and no mutable static state. The generator does
not change, wrap or replace `Result<TValue, TError>`.

## Build properties

| Property | Default | Purpose |
| --- | --- | --- |
| `ResultsSourceGenerator_Disable` | `false` | Disables generation while still emitting the opt-in attribute |

The switch reaches `PackageReference` consumers through
`buildTransitive/Purview.Results.SourceGenerator.props`, so `-p:ResultsSourceGenerator_Disable=true` (or a
`Directory.Build.props` setting) disables the helpers there too. Release tracking for the rules lives in
`src/src/SourceGenerator/AnalyzerReleases.Unshipped.md`.

## Packaging

The generator is never packed loose: the package ships its merged analyzer under `analyzers/dotnet/cs/` with no
`lib/` folder and **no PDB** (`PurviewPackAnalyzerPdb=false`), because the packaged analyzer is the framework's
ILRepack-merged assembly whose rewritten PDB carries no Roslyn compiler-flags record. The `CS0029` code fix ships
beside it as a second analyzer assembly (`Purview.Results.SourceGenerator.CodeFixes.dll`) and is never IL-merged
into the generator.

## Related

- [Union Errors](Union-Errors.md) — the union modelling this generator targets.
- [Diagnostics](Diagnostics.md) — the rules, the blocking policy and the suppression.
- [Testing](Testing.md) — the generation, cache and compiler-experiment tests.
