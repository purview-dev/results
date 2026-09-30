# Contributing

Contributions are welcome. Open an issue or a pull request against
[purview-dev/results](https://github.com/purview-dev/results).

## Repository layout

```
src/
  Results.slnx              Canonical solution for restore, build, test and pack
  src/
    Results/                Result<TValue, TError>, Result factories, IResultValue
    SourceGenerator/         Roslyn incremental generator + analyzer + [GenerateResult]
    SourceGenerator.CodeFixes/  CS0029 code fix for returning a bare union case
    AspNetCore/             Result-to-response mapping, endpoint filter, DI registration
    ZodSharp/               ZodSharp ValidationResult<T> bridge
    ZodSharp.AspNetCore/    Validation-problem mapping for validation-carrying failures
    Examples.Basic/         Runnable examples, one project per integration aspect
    Examples.Zod/
    Examples.AspNetCore/
    Examples.AspNetCore.Zod/
    <Project>/Sdk/          Package-only assets: README.md and .agents/** skills
  tests/                    TUnit unit tests (one *.UnitTests project per package)
docs/wiki/                  This documentation suite
Directory.Packages.props    Centrally managed NuGet versions
purview-build.json          Shared pipeline configuration
package.json                Authoritative repository and package version
```

## Commands

```bash
dotnet tool restore
just restore
just build
just test-unit
just lint-check
just lint-fix
just pack
just pipeline-pr
```

Local CI-equivalent validation:

```bash
dotnet restore src/Results.slnx
dotnet build src/Results.slnx --no-restore
dotnet test src/Results.slnx --no-build --treenode-filter "/*/*/*/*[Category=Unit]"
dotnet csharpier check .
bun run lint:slnx:check
```

`just lint-fix` runs CSharpier and the `.slnx` GUID cleanup. Formatting is enforced with CSharpier using the
root `.editorconfig`.

## Working agreement

1. Read `AGENTS.md`, inspect the working tree, and locate the implementation, tests, documentation and existing
   patterns relevant to the change.
2. Confirm behaviour from code and tests rather than relying on memory or documentation alone.
3. Make the smallest coherent change; preserve public behaviour unless the task explicitly changes it.
4. Update tests for fixes and behaviour changes, and documentation when public behaviour changes.
5. Run the narrowest meaningful validation first, then broader validation in proportion to risk.
6. Review the diff for unrelated edits, generated noise, compatibility risks and missing docs or tests.

## Commit conventions

Commits follow [Conventional Commits](https://www.conventionalcommits.org/), enforced by Lefthook and Commitlint
(`.config/lefthook.yml`, `commitlint.config.mts`). Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`,
`perf`, `refactor`, `revert`, `style`, `test`.

## Quality gates

- `just build` succeeds with no new warnings or errors.
- The relevant tests pass (`just test-unit`).
- `just lint-check` reports no formatting or `.slnx` changes.
- Packed packages match `purview-build.json` `PackValidation` (`just pipeline-pack-validate`).
- Generated code stays deterministic and reviewable.

## Documentation

This wiki lives in `docs/wiki/`. The purview-dev site build pulls these files through `github-path` aggregation
and rewrites relative `.md` links into site routes.

Conventions:

- `_Sidebar.md` declares page order; it is parsed by the sync and never rendered (as are `Home.md` and
  `index.md`, which the catalogue project excludes).
- Every page starts with a single `# ` heading followed by a one-paragraph description.
- Link to other pages with relative `.md` links: `[Getting Started](Getting-Started.md)`.
- GitHub alert blockquotes (`> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`, `> [!CAUTION]`, `> [!IMPORTANT]`) become
  Starlight asides.
- Relative repository-path links are rewritten to GitHub blob URLs automatically.
- Keep `README.md`, each package's `Sdk/README.md`, `AGENTS.md` and this wiki aligned with actual behaviour. If a
  change alters diagnostics, build properties, defaults, resolution order or public API, change all of them in the
  same commit.

## Packaging traps

- A project is packable **only** when its own `.csproj` declares `<IsPackable>true</IsPackable>`.
- A new union-declaring file must be added to `.csharpierignore`, because the preview syntax is not parseable.
- A new diagnostic must be added to `AnalyzerReleases.Unshipped.md`.
- A property the generator reads must be declared as a `CompilerVisibleProperty` and shipped in
  `Sdk/buildTransitive/*.props`.

## Related

- [Testing](Testing.md) — the TUnit conventions and source-generator testing approach.
- [Release Flow](Release-Flow.md) — versioning, workflows and pack validation.
- [Agent Skills](Agent-Skills.md) — the guidance the packages ship to consumers.
