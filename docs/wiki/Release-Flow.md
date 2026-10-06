# Release Flow

Releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the
GitHub Actions workflows in `.github/workflows/`. Consuming repositories own configuration through
`purview-build.json`, not pipeline source code.

## Versioning

`package.json` is the authoritative release and package version; every package is versioned from it by
`Purview.BuildSdk`. Never diverge a project's version by hand. Read the current line from `package.json`
(`just version`) rather than from this page; a prerelease uses a `MAJOR.MINOR.PATCH-prerelease.N` suffix.

## Workflows

| Workflow | Trigger | Pipeline |
| --- | --- | --- |
| `.github/workflows/pr.yml` | pull requests against `main` (opened, synchronize, reopened, ready_for_review) | shared `purview-build.yml`, with `run-pack: true` and `validate-pack: true` |
| `.github/workflows/release.yml` | push to `main` | shared `purview-release.yml` with `release-mode: NuGet` |

Both workflows pin `dotnet-version` to the SDK in `global.json` (`11.0.100-rc.1.26425.128`) — the shared workflow
defaults to the 10.0.x SDK, which cannot resolve a repository that targets `net11.0`. Keep the two workflow files
and `global.json` in sync.

The release workflow packs, publishes to NuGet, and creates the `v<version>` GitHub release only when that tag does
not already exist. Do not create release tags or publish packages manually.

## `purview-build.json`

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
- Every package ships its `README.md`, its `.agents/**` content and `purview-logo-light.png`.

The framework assembly must never appear loose beside the merged analyzer — `ForbiddenContent` rejects
`analyzers/**/Purview.SourceGeneratorFramework.dll`.

## Analyzer release tracking

The generator bundled in `Purview.Results` ships public diagnostics (`RSG1000`–`RSG1008`), so it maintains the
Roslyn release-tracking files. New or changed rules go in
`src/src/SourceGenerator/AnalyzerReleases.Unshipped.md`, which the compiler's RS2008 catalogue validates during
the build. `RSG2000` is a *suppression* id, not a reported rule, so it must not appear there.

## Commands

```bash
just pipeline-pr              # restore, build, lint, tests, pack, validate pack
just pipeline-build           # restore, build, lint (no tests, no release)
just pipeline-pack-validate   # restore, build, lint, tests, pack, validate contents
just pipeline-release         # pack, publish, GitHub release (NuGet mode)
just pipeline-local-release   # pack, publish to a local NuGet feed
just version                  # the version package.json declares
```

`pipeline-pr` and the other pipeline recipes install the pinned `Purview.Build` tool into `.tools/purview-build`
when it is missing.

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
