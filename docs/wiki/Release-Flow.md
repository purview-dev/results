# Release Flow

Releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the
GitHub Actions workflows in `.github/workflows/`. Consuming repositories own configuration through
`purview-build.json`, not pipeline source code.

## Versioning

`package.json` is the authoritative release and package version; every package is versioned from it by
`Purview.BuildSdk`. Never diverge a project's version by hand. Read the current line from `package.json`
(`just version`) rather than from this page; a prerelease uses a `MAJOR.MINOR.PATCH-prerelease.N` suffix.

## Releasing a version

A push to `main` publishes, so everything below has to be true *before* the merge.

1. **Rename the changelog heading to the version being released.** `CHANGELOG.md`'s newest `## <version>`
   heading is the one that ships; there is no `Unreleased` heading, precisely so a merge cannot publish an
   unnamed release. Anything a consumer has to act on goes under that version's `### Upgrade notes`, each note
   naming the old form, the new form and the edit to make.
2. **Move new diagnostics into a shipped block**, as [Analyzer release tracking](#analyzer-release-tracking)
   describes. A rule stays recorded under the release that *introduced* it, so earlier blocks are never
   rewritten.
3. **Bump the package-validation baseline.** `src/Directory.Build.targets` sets
   `PackageValidationBaselineVersion` to the previous stable release; it must name the version currently on
   NuGet, not the one being released. The build then fails `dotnet pack` on an unintended public-API change. An
   intended one needs a `CompatibilitySuppressions.xml` beside the owning project
   (`dotnet pack /p:GenerateCompatibilitySuppressionFile=true`) *and* a matching upgrade note — the two are each
   other's evidence.
4. **Run `just pipeline-pr`**, which covers restore, build, lint, tests, pack and pack validation in one pass.

## Workflows

| Workflow | Trigger | Pipeline |
| --- | --- | --- |
| `.github/workflows/pr.yml` | pull requests against `main` (opened, synchronize, reopened, ready_for_review) | shared `purview-build.yml`, with `run-pack: true` and `validate-pack: true` |
| `.github/workflows/release.yml` | push to `main` | shared `purview-release.yml` with `release-mode: NuGet` |

Both workflows pin `dotnet-version` to the SDK in `global.json` — the shared workflow defaults to the 10.0.x SDK,
which cannot resolve a repository that targets `net11.0`. Keep the two workflow files and `global.json` in sync,
and read the pinned version from `global.json` rather than from this page.

The release workflow packs, publishes to NuGet, and creates the `v<version>` GitHub release only when that tag does
not already exist. Do not create release tags or publish packages manually.

## `.config/purview-build.json`

| Key | Value |
| --- | --- |
| `Build:Solution` | `src/Results.slnx` |
| `Build:TestRoot` | `src/tests` |
| `Build:TestPatterns` | `*Tests.csproj` |
| `Build:TestProjects` | `*UnitTests*` |
| `Build:TestFilter` | `/*/*/*/*[Category=Unit]` |
| `PackValidation:RequireSymbolPackage` | `false` |
| `PackValidation:RequiredContent` | The exhaustive per-package content declaration |
| `Release:Mode` | `None` (publishing is enabled only by the release workflow) |

`RequireExplicitContent` defaults to `true`, so `RequiredContent` is exhaustive: a package with no rule, or an
entry matching none of its globs, fails validation. Update it whenever package content changes — a new asset, a
removed PDB, a renamed analyzer.

## Packed shapes that must not regress

- `Purview.Results` ships the merged source generator under `analyzers/dotnet/cs/` with **no `lib/` folder**
  and **no PDB**, because the packaged analyzer is the framework's ILRepack-merged assembly whose rewritten PDB
  carries no Roslyn compiler-flags record. The `CS0029` code fix ships beside it as
  `Purview.Results.SourceGenerator.CodeFixes.dll` and is never IL-merged into the generator. The generator is
  not published as a separate package.
- The library packages ship `lib/<tfm>/<assembly>.dll` plus the XML documentation file, and a symbol package.
- Every package ships its `README.md` and `purview-logo-light.png`. Agent skills are **not** packed —
  `Purview.BuildSdk` 1.0.6 removed skill packaging, and they are delivered from the repository-root `skills/`
  folder by the skills CLI instead.

The framework assembly must never appear loose beside the merged analyzer — `ForbiddenContent` rejects
`analyzers/**/Purview.SourceGeneratorFramework.dll`.

## Analyzer release tracking

The generator bundled in `Purview.Results` ships public diagnostics (`RSG1000`–`RSG1009` and `RSG9000`), so it
maintains the Roslyn release-tracking files. New or changed rules go in
`src/src/SourceGenerator/AnalyzerReleases.Unshipped.md`, which the compiler's RS2008 catalogue validates during
the build, and move into a `## Release <version>` block in `AnalyzerReleases.Shipped.md` when that version ships.
`RSG2000` is a *suppression* id, not a reported rule, so it must not appear in either file.

## Commands

```bash
just pipeline-pr              # restore, build, lint, tests, pack, validate pack
just pipeline-build           # restore, build, lint (no tests, no release)
just pipeline-pack-validate   # restore, build, lint, tests, pack, validate contents
just pipeline-release         # pack, publish, GitHub release (NuGet mode)
just pipeline-local-release   # pack, publish to a local NuGet feed
just version                  # the version package.json declares
```

`pipeline-pr` and the other pipeline recipes run `dotnet tool restore`, which installs the pinned
`Purview.Build` from `.config/dotnet-tools.json`.

## Testing the packed packages locally

A `.nupkg` published under a **new** version always extracts fresh, so the supported flow is: bump
`package.json`, publish to the local feed, then scrub the consuming repository.

In this repository, publish the new version to the local feed:

```bash
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
```

Then, in the consuming repository:

```bash
just scrub
just build
```

`LOCAL_NUGET_FEED_PATH` is exported in the development environment, so the explicit switch is only needed when it
is not. NuGet reuses an already-extracted package for the same id **and** version, so republishing the same
version requires deleting `$(NUGET_PACKAGES)/<package-id>/<version>` first — prefer a new version.

## Related

- [Contributing](Contributing.md) — the local commands and quality gates.
- [Diagnostics](Diagnostics.md) — the rules the release-tracking file lists.
