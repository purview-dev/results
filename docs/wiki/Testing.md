# Testing

Tests are **TUnit on Microsoft.Testing.Platform** only — never xUnit, NUnit, MSTest, NSubstitute or
FluentAssertions. `TUnit.Mocks` and `Bogus` are added to test projects automatically by `Purview.BuildSdk`, so
they are versioned centrally and must not be added as `PackageReference`s.

## Running the tests

```bash
just test-unit
```

That resolves to the whole solution's unit tests through the supported tree-node filter:

```bash
dotnet test src/Results.slnx --no-build --treenode-filter "/*/*/*/*[Category=Unit]"
```

The `*.UnitTests` project naming is what the SDK uses to derive `TestingType=Unit` and apply the matching test
category, which is what the filter matches. Use `--treenode-filter` for filtering — not `dotnet test --filter`.

## Test projects

| Project | Covers |
| --- | --- |
| `src/tests/Results.UnitTests` | `Result<TValue, TError>` and `Result<TError>` states, the throw-on-misuse contract, and the extension operations (`ResultsTests`, `ResultTErrorTests`, `ResultExtensionsTests`) |
| `src/tests/SourceGenerator.UnitTests` | Generation, diagnostics, incremental caching, the compiler experiments, the code fix, and the suppressor |
| `src/tests/AspNetCore.UnitTests` | `DefaultResultsHttpMapper`, the endpoint filter, failure mappers and unmapped-failure behaviour |
| `src/tests/ZodSharp.UnitTests` | `ToResult` and the validation-result integration |
| `src/tests/ZodSharp.AspNetCore.UnitTests` | `ZodResultsFailureMapper`, registration order and the validation problem mapper |

## Conventions

- Test naming is `{SubjectUnderTest}_{Scenario}_{Expectation}`, for example
  `DefaultResult_ShouldBeUninitialized`, `MapError_WhenFailure_ShouldTransformError`,
  `GenerateIncrementalAsync_GivenFirstRun_MarksEveryStageNew`.
- One `{Class}Tests` class per class under test, in the same namespace.
- `public async Task` with a `CancellationToken` whenever the API under test accepts one.
- Mock with `TUnit.Mocks`; do not introduce a substitute framework.

## Source-generator tests

Generator tests derive from `TUnitSourceGeneratorTestBase<TGenerator, TOptions>` with an options record that seeds
the required namespaces and assemblies; `ResultsSourceGeneratorTestOptions` is the reference. Assert on generated
code with the framework's `CodeQuery` and terminal assertion extensions (`result.Generated()`,
`HasGeneratedSyntaxTree`, `HasGeneratedClass`) rather than raw strings.

**Incremental pipelines need stage-by-stage cache tests.** `ResultsSourceGeneratorCacheTests` is the reference:
it drives `GenerateIncrementalAsync`, asserts `New` on the first run, `Cached`/`Unchanged` on an identical rerun,
and `Modified` only for the stages whose inputs changed.

## Compiler experiments

Behaviour that motivates a design decision is recorded as a compiler experiment rather than an assumption.
`UnionCompilerBehaviourTests` proves the `CS0029`/`CS1929` cases the generator exists to work around, and the
`CS0715`/`CS0556`/`CS9282`/`CS0246` cases that rule out generated implicit conversions. When a language claim in
these docs changes, the experiment is where it is verified.

## Code-fix tests

A code fix that answers a **compiler** diagnostic cannot use `TUnitCodeFixTestBase`, because that base is driven
by an analyzer's diagnostics. `UnionCodeFixTestHarness` instead runs the generator, applies the fix through an
`AdhocWorkspace`, and recompiles the rewritten source so a broken fix cannot pass. A `Document` is an immutable
snapshot, so the harness re-resolves it from the workspace's current solution after adding documents.

## Suppressor tests

The real `CA1815` comes from the .NET analyzers the SDK loads, which a unit-test compilation cannot reference, so
`UnionEqualityDiagnosticSuppressorTests` reports the same id at the same location from a test-only analyzer
(`Ca1815ReporterAnalyzer`) and runs it beside the shipped suppressor through `CompilationWithAnalyzers`. The
compilation under test is still real: it is produced by running the generator, so the union, the generated
attribute and the generated helpers are all present.

## Related

- [Source Generator](Source-Generator.md) — the pipeline the cache tests guard.
- [Diagnostics](Diagnostics.md) — the rules the analyzer tests assert.
- [Contributing](Contributing.md) — the day-to-day command set.
