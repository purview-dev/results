# Changelog

All notable changes to this repository are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). `package.json` is the authoritative version; see
[Release Flow](docs/wiki/Release-Flow.md).

Released versions correspond to `v<version>` GitHub releases. Entries below the `Unreleased` heading have not
been published to NuGet.

## Unreleased

### Added

- The value-less `Result<TError>` type: the same three-state `readonly record struct` contract as
  `Result<TValue, TError>`, but a success holds the stateless `Success` marker instead of a value. Comes with
  `Match`, `Switch`, `Tap`, `TapError`, `Ensure`, `Map`, `Bind`, `MapError`, `MapAsync`, `BindAsync`, `OrElse`,
  `TryGetError`, `GetErrorOrDefault` and `Throw()`, plus implicit conversions from `Success` and `TError`.
  `Result<TValue, TError>.DiscardValue()` bridges a value result to a unit result.
- `Purview.Results.ZodSharp` integration with `Purview.ValueObjects`, so a type-level `[ZodRule]` carries its
  code and origin through a result and into the HTTP mapping. Demonstrated by the new
  `Examples.ValueObjects.Zod` example.
- Union inclusion: a union-typed case is expanded automatically, so an including union's generated factory also
  covers every case reachable through it, constructing the nested value. This is the supported way to avoid
  `RSG1006` without flattening a union.
- `RSG1008`, reported when a case is reachable through two different included unions, where no single
  construction path can be named.
- `RSG1009`, reported when a union case is not a named type (an array, for example) so no helper or factory
  can be declared against it.
- `RSG9000`, reported when the generator fails unexpectedly, instead of letting the exception escape.
- The union-receiver factory as a C# 14 extension block on the union type — `Union.Failure(case)`,
  `Union.Failure<TValue>(case)`, `Union.Success()` and `Union.Success<TValue>(value)`. Because it names the union
  by its receiver, it is safe for cases shared between unions, and the code fix rewrites `CS0029` to it.
- Examples recipe `just example-valueobjects-zod`.

### Changed

- **Consuming these packages no longer forces preview features on your solution.** `EnablePreviewFeatures`
  was set repo-wide, which emitted `[assembly: RequiresPreviewFeatures]` into all four shipped assemblies.
  Because that attribute is assembly-wide, `CA2252` — an **error** by default — fired on every member a
  consumer touched, including `Result<TValue, TError>`, which uses no union feature. A consuming application
  could not adopt the result type at all without enabling preview features solution-wide, so partial adoption
  was impossible. The property was unnecessary: union declarations are a *language* feature needing only
  `LangVersion=preview`, and nothing here uses a runtime API annotated `[RequiresPreviewFeatures]` — every
  project, including the union-declaring examples and tests, compiles without it.

  The requirement is now scoped and per-project:
  - Using the result types and combinators needs **nothing special**.
  - Declaring your own `[GenerateResult]` union needs `LangVersion=preview` in **that** project.

  Both paths are verified against the packed package, the first with `TreatWarningsAsErrors=true`.
- **Breaking:** the generated `[GenerateResult]` opt-in attribute moved from `Purview.Results.SourceGenerator`
  to `Purview.Results`. Remove any `using Purview.Results.SourceGenerator;` added for the attribute.
- **All four packages are now marked `IsAotCompatible`**, enabling `IsTrimmable` and both the trim and AOT
  analyzers, and all four are verified clean. For `Purview.Results` and `Purview.Results.ZodSharp` this
  independently confirms the "no reflection, no dynamic, no runtime type discovery" guarantee.
- **The ASP.NET Core success path no longer erases the value's type.** `DefaultResultsHttpMapper` serialized
  through `IResultValue.SuccessValue`, which is `object?`, so `TypedResults.Ok` bound `Ok<object>`. Under
  trimming or Native AOT that leaves the serializer with no `JsonTypeInfo` to resolve, and it also erased the
  response type ASP.NET Core infers for OpenAPI. The value now reaches `TypedResults` with its static type
  through the new `IResultValue.AcceptSuccess` double dispatch.

### Added — observability

- **Metrics under the meter `Purview.Results.AspNetCore`**: `purview.results.failures.mapped`,
  `purview.results.failures.unmapped` and `purview.results.uninitialized`. The design calls an unmapped
  failure a host mapping gap rather than a domain outcome, but the only signal was a log message — so
  answering "how often, and for which error" meant scraping log text. Each measurement carries a
  `purview.results.error_type` tag, and the failure paths add the same tag to `Activity.Current`, enriching
  the request span ASP.NET Core already started rather than creating one. Register with
  `metrics.AddMeter(ResultsHttpTelemetry.MeterName)`.
- The error type is **always** present in telemetry, because metrics and traces stay inside your own
  infrastructure. Disclosing it in the HTTP *response* remains opt-in via
  `IncludeErrorTypeInProblemDetails`, so a mapping gap stays diagnosable without being published.
- **Stable `EventId`s** on every log statement, via source-generated `LoggerMessage`: `1000` uninitialized
  result, `1001` unmapped failure, `2000` a ZodSharp rule answering a multi-error failure. Previously none
  had an identifier, so alerting meant matching message text. The generated path also removes the
  hand-written `IsEnabled` guard the Debug call site needed.
- No telemetry package dependency is added: `Meter` and `Activity` come from the base class library through
  the ASP.NET Core framework reference, so the repository's dependency-light position is unchanged.

### Added — .NET 10 support

- **All four packages now multi-target `net10.0` and `net11.0`**, so consumers choose their runtime rather
  than being forced onto .NET 11. .NET 8 and 9 are deliberately not targeted — both are close to end of
  support. Verified end to end: a `net10.0` console consumer and a `net10.0` ASP.NET Core minimal API both
  build and run against the packed packages, with `TreatWarningsAsErrors`.
- **Unions still require .NET 11**, and that is a compiler constraint rather than a choice: a union
  declaration needs `System.Runtime.CompilerServices.IUnion` and `UnionAttribute`, neither of which exists
  before .NET 11. Everything else works on `net10.0` — the result types, the combinators, the ASP.NET Core
  mapping and the ZodSharp bridge. The HTTP mapper's union unwrapping is compiled only for `net11.0`; on
  `net10.0` an error is its own single case, which is the answer the union path already gives for a
  non-union error. Projects that declare a union (the examples and the union-declaring test projects) are
  pinned to `net11.0`.
- `Results.UnitTests` multi-targets, so the core is now exercised on both runtimes — the suite went from
  342 to 492 executions.
- **Runtime async is scoped to `net11.0`.** `Features=runtime-async=on` was set repo-wide. On `net10.0` it
  lowers `await` to `System.Runtime.CompilerServices.AsyncHelpers`, which is evaluation-only and fails the
  build with `SYSLIB5007` — the API is explicitly subject to change or removal. A `net10.0` consumer
  therefore gets the stable async lowering.

### Changed — analyzer release tracking

- **Every diagnostic moved from `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md`** under
  `## Release 1.0.0`. There was no `Shipped.md` at all, so the catalogue claimed nothing had ever shipped;
  all eleven rules (`RSG1000`–`RSG1009`, `RSG9000`) are now recorded. `RSG2000` is deliberately absent from
  both files: it is a suppression id, and suppression descriptors are not release-tracked.

### Added — `IResultValueVisitor<TState, TReturn>`

- A new visitor interface and `IResultValue.AcceptSuccess<TState, TReturn>` hand a result's successful value
  to infrastructure **with its static type intact**, without reflection. `SuccessValue` remains the right
  shape for inspection; use `AcceptSuccess` whenever the type matters, such as serialization.
- **Breaking for anyone implementing `IResultValue` themselves** (the two result types implement it in-box;
  external implementations are not expected).
- Async combinators now await with `ConfigureAwait(false)`, so they no longer resume on a captured
  synchronization context. This affects consumers that relied on continuation back onto a UI or legacy
  ASP.NET context.

### Fixed

- **An array-typed union case crashed the generator and discarded every other union's output.** The
  discovery code explicitly handles `IArrayTypeSymbol` when checking accessibility and type parameters, then
  cast the case to `INamedTypeSymbol` — which an array is not — throwing `InvalidCastException`. With no
  catch-all in the pipeline the compiler reported `CS8785` and dropped **all** generated source for the
  compilation: every `AsFailure` helper and union factory vanished, producing a cascade of unrelated
  `CS1061`/`CS0103` errors with no indication of the cause, and `AD0001` in the IDE where analysis then
  stopped. A single malformed union anywhere in a solution did this. Now reported as `RSG1009` and skipped;
  the union's other cases still generate.
- **Added a generator catch-all (`RSG9000`).** An unexpected failure is now reported against the
  compilation rather than allowed to escape, so the rest of the generated output survives and the cause is
  identifiable. `OperationCanceledException` is rethrown, because the IDE cancels generation on every
  keystroke.

### Security

- The source generator is now bundled into the `Purview.Results` package correctly. `Purview.Results` ships both
  the IL-merged generator and `Purview.Results.SourceGenerator.CodeFixes.dll` under `analyzers/dotnet/cs/`.

### Documentation

- Recorded that results have **no serialization support** and that reflection-based `System.Text.Json`
  serialization throws, in [Guarantees and Limitations](docs/wiki/Guarantees-and-Limitations.md).
- Added XML documentation to the four public implicit conversion operators.
- Corrected the `Examples.*` paths in the `Justfile` (`src/src`, not `src/examples`), the packable-project count
  in `pr.yml`, the `pipeline-local-release` status in `AGENTS.md`, and the hardcoded version in
  [Release Flow](docs/wiki/Release-Flow.md).

## 1.0.0-prerelease.2

See the [`v1.0.0-prerelease.2`](https://github.com/purview-dev/results/releases/tag/v1.0.0-prerelease.2)
release notes.

## 1.0.0-prerelease.1

Initial prerelease: `Result<TValue, TError>`, the `[GenerateResult]` source generator, the `CS0029` code fix,
and the ZodSharp and ASP.NET Core integrations. See the
[`v1.0.0-prerelease.1`](https://github.com/purview-dev/results/releases/tag/v1.0.0-prerelease.1) release notes.
