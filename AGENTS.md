# Agent Instructions

## Purpose and authority

This repository contains the Purview result types for .NET: `Purview.Results`, a Roslyn source generator that
makes C# 15 union error cases ergonomic, and the ZodSharp and ASP.NET Core integrations.

- This file is the repository-wide source of truth for AI agents. A more-specific `AGENTS.md` in a subtree, if
  one is ever added, takes precedence for that subtree.
- `.github/copilot-instructions.md` defers to this file.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then established
  code patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles, local
  configuration, test output, or provider credentials.

## Source of truth and layout

| Path | Purpose |
| --- | --- |
| `src/Results.slnx` | Canonical solution for restore, build, test and pack |
| `src/src/Results` | `Result<TValue, TError>`, the `Result` factories, `IResultValue` |
| `src/src/SourceGenerator` | Roslyn incremental generator, diagnostic analyzer, `[GenerateResult]` attribute |
| `src/src/SourceGenerator.CodeFixes` | IDE code fix for `CS0029`: rewrites a returned union case into the generated `AsFailure<TValue>()` |
| `src/src/AspNetCore` | Result-to-response mapping, endpoint filter, DI registration |
| `src/src/ZodSharp` | ZodSharp `ValidationResult<T>` bridge |
| `src/src/ZodSharp.AspNetCore` | Validation-problem mapping for validation-carrying failures |
| `src/src/<Project>/Sdk` | Package-only assets. `Sdk/README.md` is packed as the package README; `Sdk/.agents/**` would ship agent skills with the package |
| `src/tests` | TUnit unit tests, including source-generation and incremental-cache tests |
| `src/examples` | Runnable, non-packable examples: one project per integration aspect on a shared Tenant* domain |
| `Directory.Packages.props` | Centrally managed NuGet versions |
| `src/Directory.Build.props` / `src/Directory.Build.targets` | Solution-wide SDK, package and build behaviour |
| `global.json` | Required .NET SDK, `Purview.BuildSdk` and Microsoft.Testing.Platform selection |
| `package.json` | Authoritative repository and package version |
| `purview-build.json` | Shared `Purview.Build` pipeline configuration (solution, test discovery, test filter) |
| `Justfile` | Supported local workflow commands |
| `.agents` | Package-delivered skills, agents and prompts (see [Agent skills](#agent-skills)) |
| `.purview/agent-sync.cache` | SDK-managed manifest recording which `.agents` files are already mirrored |

## Standard workflow

1. Read this file, inspect the working tree, and locate the implementation, tests, documentation and existing
   patterns relevant to the task.
2. Confirm behaviour from code and tests rather than relying on memory or documentation alone.
3. Make the smallest coherent change. Preserve public behaviour unless the task explicitly changes it.
4. Update tests for fixes and behaviour changes. Update documentation when public behaviour changes.
5. Run the narrowest meaningful validation first, then broader validation in proportion to risk.
6. Review the diff for unrelated edits, generated noise, compatibility risks, and missing docs or tests.

## Runtime invariants

- `Result<TValue, TError>` is a `readonly record struct` with three states: `Uninitialized` (the `default`
  value), `Success` and `Failure`. Keep all three observable through `IsInitialized`, `IsSuccess` and
  `IsFailure`.
- Keep the throw-on-misuse contract: `Value` and `Error` throw `InvalidOperationException` with the existing
  messages in the wrong state, and `Match`, `Map`, `Bind` and `MapError` throw `"The result is uninitialized."`
  for `default`. Never silently coerce an uninitialized result into a success or a failure.
- `ToString()` stays `Success(value)` / `Failure(error)` / `Uninitialized`.
- `IResultValue` is the non-generic view used by infrastructure. Its accessors must never throw; the accessor
  that does not describe the current state returns `null`.
- Implicit conversions from `TValue` and `TError`, and the `Result.Success`/`Result.Failure` and
  `Result<TValue, TError>.Success`/`.Failure` factories, are public contract.
- `Purview.Results` stays dependency-free. The ZodSharp and ASP.NET Core packages depend on it, never the
  reverse, and `Purview.Results.AspNetCore` deliberately knows nothing about ZodSharp.
- Keep runtime packages free of reflection, `dynamic` and runtime type discovery. Union structure is inspected
  only by the source generator, through Roslyn symbols.
- `src/src/Results/Extensions/Purview/Results/ResultExtensions.cs` is the home for value-style extension
  methods over a result. Add extension methods there rather than on the result type itself.

## Source generator rules

`src/src/SourceGenerator/Sdk/README.md` is the authoritative design document — keep it in sync with behaviour,
including the diagnostics table, build properties and activation rules.

- **One analyzer, one generator, one diagnostics library.** `Diagnostics/DiagnosticLibrary.cs`,
  `Diagnostics/ResultDiagnostic.cs` and `Diagnostics/ResultUnionDiagnostics.cs` hold the single implementation
  of the union rules. The analyzer reports the per-target rules (`RSG1000`–`RSG1004`, `RSG1007`); the generator
  reports the compilation-wide rules (`RSG1005`, `RSG1006`). Never report the same rule from both hosts, and
  keep `DiagnosticLibrary.IsBlocking` as the single blocking policy.
- New or changed rules require an `AnalyzerReleases.Unshipped.md` entry (the compiler's RS2008 rule catalogue).
- **Keep the pipeline value-equatable.** Do not let `ISymbol`, `Compilation`, `SemanticModel`, `IOperation`,
  `SyntaxNode`, `SyntaxTree` or `Location` reach cached models; convert them during discovery
  (`ResultUnionModel`, `ResultUnionCaseModel`, `ResultSourceLocation`) and use `EquatableArray<T>`.
- **Generate deterministically.** Cases are ordered ordinally by fully-qualified name, unions are processed in
  ordinal order, hint names derive from the union's identity, and nothing emits timestamps, GUIDs or machine
  paths.
- **Accessibility never widens.** The generated class is `public` only when the union and every case type it
  references are visible; otherwise `internal`.
- The component targets `netstandard2.0` (with the SDK's Roslyn defaults). Do not use APIs that target does not
  have.
- Create the `CodeWriter` inside the `RegisterSourceOutput` callback; never store it in incremental pipeline
  state or a custom context.
- `ResultsSourceGenerator_Disable` must stay declared as both a `PurviewGeneratorVisibleProperty` and a
  `CompilerVisibleProperty`, and shipped to consumers by
  `src/src/SourceGenerator/Sdk/buildTransitive/Purview.Results.SourceGenerator.props`. The framework's PSGF0003
  validation fails the build when a declared generator-read property is neither compiler-visible nor declared by
  the package's own `Sdk/build*` assets, which is what keeps the packaged switch honest.
- `[GenerateResult]` is supported on union declarations only. Generic unions and `IUnionMembers` member
  providers are deliberately unsupported and reported as `RSG1002`/`RSG1007`.
- **Do not attempt to generate implicit conversions.** C# blocks every route: an operator is illegal in a static
  class (`CS0715`), a conversion operator must be declared by the source or target type (`CS0556`), extension
  members cannot declare conversions (`CS9282`), an operator cannot declare its own type parameters so `TValue`
  is unavailable for a non-generic case type (`CS0246`), and only one user-defined conversion may participate in
  a sequence (`CS0029`). All five are recorded in `UnionCompilerBehaviourTests.cs`. Improve call-site
  ergonomics through the code fix in `src/src/SourceGenerator.CodeFixes` instead.
- Adding a code fix means adding it to `src/src/SourceGenerator.CodeFixes`, not to the generator project: the
  code fix needs `Microsoft.CodeAnalysis.*.Workspaces`, which must not enter the generator's analyzer closure.
  The two projects stay independent (no project reference, no `InternalsVisibleTo`) so no reference cycle can
  form, and the shared well-known names are pinned by the code-fix tests.
- Union-declaring test files are excluded from CSharpier through `.csharpierignore`, because the preview syntax
  is not parseable. Add any new file that declares a union to that list.

## ASP.NET Core integration invariants

- Keep the failure resolution order in `DefaultResultsHttpMapper`: the mapping for the error **case** type,
  then the mapping for the **error** type (which covers every case without its own mapping), then the
  registered fallbacks in order, then the unmapped-failure response.
- An unmapped failure is a host mapping gap, not a domain outcome: respond with `UnmappedStatusCode` (`500`)
  and a `ProblemDetails` carrying the `errorType` extension, and log an error. `ThrowOnUnmappedFailure` exists
  for development and must keep throwing `InvalidOperationException`.
- An uninitialized result (`default`) takes the same path and is logged, because an endpoint returning `default`
  is a bug.
- `AddResultsHttp` registers problem-details services and uses `TryAddSingleton` for `IResultsHttpMapper` and
  `ResultsEndpointFilter`, so a host can register its own mapper first and replace the defaults.
- `WithResultsHttp()` exists on both `RouteHandlerBuilder` and `RouteGroupBuilder`; keep the endpoint filter
  non-invasive, so a handler that returns something other than an `IResultValue` is left untouched.
- `Purview.Results.ZodSharp.AspNetCore` registers its mapping as a `ResultsHttpOptions.AddFallback`, so any
  case- or error-type mapping the host registered for a specific error always wins. It must reuse the ZodSharp
  problem mapper (`ZodValidationProblems.ToProblem`) rather than reimplementing error-to-problem mapping, so a
  result-carried validation failure and a thrown `ZodException` produce identical responses.

## Packaging rules

- A project is packable **only** when its own `.csproj` declares `<IsPackable>true</IsPackable>`. The SDK reads
  that declaration from the project file text, so never move it into `Directory.Build.props`: doing so packs
  nothing and the release pipeline silently produces no packages.
- Shared package metadata (authors, company, copyright, project URL, icon, README file name, MIT licence
  expression) lives in `src/Directory.Build.props` and applies to every packable project.
- A property the generator reads from build configuration must reach consumers as a `Sdk/build*/*.props` (or
  `Sdk/buildTransitive/*.props`) file declaring a `CompilerVisibleProperty`; a declaration inside the repository
  alone is invisible to `PackageReference` consumers, so the documented switch would silently do nothing.
- Per-package documentation is `src/src/<Project>/Sdk/README.md`. The SDK packs the project's `Sdk/**` folder
  into the package root, so `Sdk/README.md` becomes the `.nupkg` README and takes precedence over the
  repository-root `README.md`. Update it for any user-visible change, and keep the repository-root `README.md`
  consistent with it.
- The packed shapes that must not regress: the Roslyn component ships its analyzer assembly under
  `analyzers/dotnet/cs/` with no `lib/` folder and **no PDB** (`PurviewPackAnalyzerPdb=false`), and the library
  packages ship `lib/<tfm>/<assembly>.dll` plus the XML documentation file and a symbol package.
- The component opts out of the analyzer PDB because the packaged analyzer is the framework's merged (ILRepack)
  assembly: the merge tool's rewritten PDB carries no Roslyn compiler-flags record, and the pipeline's pack
  validation asserts `optimization=release` for every assembly that ships a PDB, with no way to scope that check
  to one package. Telemetry and the other component packages opt out the same way. Revisit if the framework's
  merge tool starts preserving the compiler-flags record.
- `purview-build.json`'s `PackValidation.RequiredContent` is the exhaustive declaration of what each package
  ships (`RequireExplicitContent` defaults to `true`, so an undeclared entry fails too). Update it whenever
  package content changes — a new asset, a removed PDB, a renamed analyzer — and verify with
  `just pipeline-pack-validate`, which runs restore, build, lint, tests, pack and the validation.
- `Purview.Results.SourceGenerator` therefore carries **two** analyzer assemblies in `analyzers/dotnet/cs/`:
  the merged generator and `Purview.Results.SourceGenerator.CodeFixes.dll`. The code fix is packed by the SDK's
  `PackProjectReferencedSourceGenerators` from the analyzer project reference in `SourceGenerator.csproj`, and it
  is never IL-merged into the generator. Verify both are present whenever analyzers or packaging change.
- Verify packaging by packing and inspecting the output, for example
  `dotnet pack src/Results.slnx -o <folder>` followed by opening the `.nupkg`. The PR and release pipelines run
  the same check through `Purview.Build`'s pack validation.

## Testing conventions

- TUnit on Microsoft.Testing.Platform only — never xUnit, NUnit, MSTest, NSubstitute or FluentAssertions.
- `TUnit.Mocks` and `Bogus` are added to test projects automatically by `Purview.BuildSdk` and are versioned
  centrally: do not add them as `PackageReference`s in a project, and do not remove their `PackageVersion`
  entries from `Directory.Packages.props`.
- Keep the `*.UnitTests` project naming. The SDK derives `TestingType=Unit` from the project name and applies
  the matching test category, which is what the `/*/*/*/*[Category=Unit]` filter matches.
- Use `--treenode-filter` for filtering, not `dotnet test --filter`. Tests live under `src/tests`, so test
  projects declare just the references they need and rely on the SDK for TUnit, coverage and test-host wiring.
- Mock with `TUnit.Mocks`; do not introduce a substitute framework.
- Test naming is `{SubjectUnderTest}_{Scenario}_{Expectation}` (`DefaultResult_ShouldBeUninitialized`,
  `MapError_WhenFailure_ShouldTransformError`, `GenerateIncrementalAsync_GivenFirstRun_MarksEveryStageNew`), one
  `{Class}Tests` class per class under test in the same namespace, and `public async Task` with a
  `CancellationToken` whenever the API under test accepts one.
- Source-generator tests derive from
  `TUnitSourceGeneratorTestBase<TGenerator, TOptions>` with an options record that seeds the required
  namespaces and assemblies (`ResultsSourceGeneratorTestOptions` is the reference). Assert on generated code
  with the framework's `CodeQuery` and terminal assertion extensions (`result.Generated()`,
  `HasGeneratedSyntaxTree`, `HasGeneratedClass`) rather than raw strings.
- Incremental pipelines need stage-by-stage cache tests built on `GenerateIncrementalAsync`, asserting `New` on
  the first run, `Cached`/`Unchanged` on an identical rerun, and `Modified` only for the stages whose inputs
  changed. `ResultsSourceGeneratorCacheTests.cs` is the reference implementation.
- Record behaviour that motivates a design decision as a compiler experiment rather than an assumption —
  `UnionCompilerBehaviourTests.cs` proves the `CS0029`/`CS1929` cases the generator exists to work around, and
  the `CS0715`/`CS0556`/`CS9282`/`CS0246` cases that rule out generated implicit conversions.
- A code fix that answers a **compiler** diagnostic cannot use `TUnitCodeFixTestBase`, because that base is
  driven by an analyzer's diagnostics. Follow `UnionCodeFixTestHarness` instead: it runs the generator, applies
  the fix through an `AdhocWorkspace`, and recompiles the rewrite so a broken fix cannot pass. A `Document` is an
  immutable snapshot, so re-resolve it from the workspace's current solution after adding documents.

## Examples

`src/examples` holds one runnable example per integration aspect, all on the Tenant* domain the READMEs
document: `Examples.Basic` (the result type and the generated helpers), `Examples.Zod`
(`Purview.Results.ZodSharp`), `Examples.AspNetCore` (`Purview.Results.AspNetCore`) and `Examples.AspNetCore.Zod`
(`Purview.Results.ZodSharp.AspNetCore`).

- Examples are **documentation that compiles**: keep them non-packable (never declare
  `<IsPackable>true</IsPackable>`), keep them out of test discovery (no `*Tests` suffix, and they are not under
  `src/tests`), and keep each one's references equal to exactly the aspect it demonstrates.
- Each example declares its own local union rather than sharing a domain project, matching how the test
  projects declare `HttpTestError`/`ValidationTestError`. Anything that declares a union must be added to
  `.csharpierignore`.
- The generator is referenced analyzer-style (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`,
  `PrivateAssets="all"`), exactly as the test projects reference it.
- When public behaviour changes, update the example that demonstrates it, the example's snippet in the root
  `README.md`, and the matching `Sdk/README.md` pointer in the same commit.

## Documentation rules

- Every public member carries XML documentation. Packable projects generate documentation files, so treat
  missing or stale XML docs as a build-quality problem rather than a nicety.
- Document behaviour that is not obvious from a signature in `<remarks>`, and prefer a compiling `<example>` or
  `<code>` block for anything a consumer has to get right — the ZodSharp bridge and the ASP.NET Core mapper are
  the existing models for this.
- Keep each package `Sdk/README.md`, the repository-root `README.md`, `AGENTS.md` and the code aligned. If a
  change alters diagnostics, build properties, defaults, resolution order or public API, change all of them in
  the same commit.
- Examples must compile conceptually against the current public API. Use placeholders for credentials and
  environment-specific values.

## Agent skills

The repository-root `.agents/**` tree is **delivered by NuGet packages**; this repository also **authors** its
own agent content under `src/src/<Project>/Sdk/.agents/**`, which the packages ship to consumers.

`Purview.BuildSdk` mirrors the `.agents` folder of every restored package into the repository root before build
and records what it copied in `.purview/agent-sync.cache`, so the root tree is complete even though it is not
maintained here.

### Authored here (shipped by this repository's packages)

| Content | Package | Path |
| --- | --- | --- |
| `skills/purview-results-core` | `Purview.Results` | `src/src/Results/Sdk/.agents` |
| `skills/purview-results-union-errors`, `agents/purview-results-union-author.agent.md`, `prompts/migrate-error-returns-to-result-unions.prompt.md` | `Purview.Results.SourceGenerator` | `src/src/SourceGenerator/Sdk/.agents` |
| `skills/purview-results-http-mapping` | `Purview.Results.AspNetCore` | `src/src/AspNetCore/Sdk/.agents` |
| `skills/purview-results-zodsharp-validation` | `Purview.Results.ZodSharp` | `src/src/ZodSharp/Sdk/.agents` |
| `skills/purview-results-zodsharp-problems` | `Purview.Results.ZodSharp.AspNetCore` | `src/src/ZodSharp.AspNetCore/Sdk/.agents` |

- These files **are tracked source** and are packed as `.agents/**` (the SDK also generates the per-skill
  `.gitignore` inside the package). Update them like any other package asset, and keep each skill's guidance in
  step with the code it documents.
- A consuming repository that imports `Purview.BuildSdk` receives them in its own `.agents/` folder on the next
  restore/build, so consumer-facing guidance is written for consumers (package-facing, not repository-facing).
- Load the relevant skill before doing the work it covers:
  - modelling or migrating to result error unions → `purview-results-union-errors`
  - the result type, its states or its combinators → `purview-results-core`
  - result-to-HTTP mapping or unmapped-failure `500`s → `purview-results-http-mapping`
  - ZodSharp validation flowing through results → `purview-results-zodsharp-validation`, then
    `purview-results-zodsharp-problems`

### Delivered by upstream packages (re-synced, do not edit)

| Content | Source package |
| --- | --- |
| `skills/project-placement-defaults`, `skills/sdk-configuration-reference`, `skills/sdk-project-behavior-and-detection`, `agents/sdk-consumer-setup.md`, `prompts/sdk-diagnose-agent-folder-copy.md` | `Purview.BuildSdk` |
| `skills/source-generator-codewriter-modernization`, `agents/source-generator-framework-writer.agent.md`, `prompts/refactor-source-generator-to-codewriter.prompt.md` | `Purview.SourceGeneratorFramework` |
| `skills/source-generator-testing` | `Purview.SourceGeneratorFramework.Testing` |
| `skills/tunit-test-authoring`, `agents/test-author-writer.agent.md`, `prompts/modernize-test-to-codequery-tunit.prompt.md` | `Purview.SourceGeneratorFramework.Testing.TUnit` |

- **Upstream skills are not tracked by git.** Each `.agents/skills/<name>/` folder carries an SDK-generated
  `.gitignore` that ignores everything except itself, so only those stub files appear in `git status`. Treat that
  content as read-only: do not edit it here and do not force-add it to git. Changes belong in the owning package
  repository (`build-sdk`, `sourcegenerator-framework`).
- Upstream agents and prompts under `.agents/agents/**` and `.agents/prompts/**` are tracked, but they are still
  package-owned: editing them locally is overwritten by the next restore or build.
- If `.agents` content looks missing or stale after a package bump, run a build and inspect
  `.purview/agent-sync.cache`; deleting an entry (or the manifest) forces a fresh copy.

## Build, format, test and pack commands

Prefer the `Justfile` recipes or their equivalent commands:

```text
dotnet tool restore
just restore
just build
just test-unit
just lint-check
just pack
just pipeline-pr
```

Local CI-equivalent validation:

```text
dotnet restore src/Results.slnx
dotnet build src/Results.slnx --no-restore
dotnet test src/Results.slnx --no-build --treenode-filter "/*/*/*/*[Category=Unit]"
dotnet csharpier check .
bun run lint:slnx:check
```

Use `just lint-fix` (CSharpier format plus the `.slnx` GUID cleanup) to fix formatting, and pack whenever
package assets, dependencies, analyzers or packaging metadata change. `TUnit.Mocks` and `Bogus` are pinned in
`Directory.Packages.props` precisely because the SDK references them implicitly for test projects.

### Testing the packed packages locally

A `.nupkg` published under a **new** version always extracts fresh, so the supported flow is: bump
`package.json`, publish to the local feed, then scrub the consuming repository.

```text
# in this repository
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/

# in the consuming repository
just scrub
just build
```

`pipeline-local-release` runs the shared pipeline with `Release:Mode=LocalNuGet`. It is present in this
repository's `Justfile` but currently commented out; until it is re-enabled, run the same command directly
(after `just ensure-pipeline-tool`):

```text
.tools/purview-build/purview-build --Release:Mode=LocalNuGet --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
```

`LOCAL_NUGET_FEED_PATH` is exported in this environment, so the explicit `--PublishLocalNuGet:LocalFeedPath`
argument is only needed when it is not. `just scrub` deletes every `bin`/`obj`, cleans, re-restores with
`--force-evaluate` and shuts the build server down, so the consumer cannot reuse stale restore or compiler
state.

NuGet reuses an already-extracted package for the same id **and** version, which is the one case these recipes
do not cover: republishing the same version, or repacking it to validate assets by hand, requires deleting
`$(NUGET_PACKAGES)/<package-id>/<version>` first (`dotnet nuget locals global-packages --clear` clears them
all). Prefer a new version, as the local release flow does.

## Versioning and releases

- `package.json` is the authoritative release and package version. Do not manually diverge project versions.
- Release is automatic on push to `main`: `.github/workflows/release.yml` calls the shared
  `purview-dev/build` release pipeline with `Release:Mode=NuGet`, which packs, publishes and creates the
  `v<version>` GitHub release only when that tag does not already exist.
- `.github/workflows/pr.yml` runs the shared build pipeline for pull requests, pinned to the SDK version in
  `global.json` so the runner can resolve it.
- Never create release tags or publish packages manually unless the user explicitly requests a documented
  recovery procedure.
- `.github/workflows/*` and `global.json` must stay in sync: the workflow's `dotnet-version` input has to match
  `sdk.version`, and `purview-build.json` has to keep pointing at `src/Results.slnx`.

## Completion checklist

Before handing work back:

- Confirm the requested behaviour and scope are satisfied.
- Confirm only intended files changed.
- Review public API, package-content and dependency-direction implications.
- Add or update focused tests for code changes, including incremental-cache coverage for pipeline changes.
- Update the affected package `Sdk/README.md`, the root `README.md`, `AGENTS.md` and `AnalyzerReleases` when the
  change affects them.
- Watch for the packaging traps: `<IsPackable>true</IsPackable>` missing from a new package project, a new
  union-declaring file missing from `.csharpierignore`, and a new diagnostic missing from
  `AnalyzerReleases.Unshipped.md`.
- Run appropriate build, test, formatting and pack checks in proportion to risk.
- State exactly what validation ran. If a check was skipped or blocked, give the concrete reason and the
  remaining risk.
