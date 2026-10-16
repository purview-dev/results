# Agent Skills

This repository authors **agent skills** so AI agents working with `Purview.Results` get the guidance
automatically. They are authored at `skills/<name>/SKILL.md` and installed with the skills CLI, which clones
this git repository — **no package ships them**. `Purview.BuildSdk` 1.0.6 removed skill packaging and the
build-time `.agents/` mirror, so the CLI is the only delivery route.

## Skills published by this repository

| Skill | Covers |
| --- | --- |
| `skills/purview-results-core` | The result type, its three states, combinators and the throw-on-misuse contract |
| `skills/purview-results-union-errors` | Union modelling, the generated helpers, the diagnostics table, and the language rules that make the helper necessary |
| `skills/purview-results-http-mapping` | Result-to-HTTP mapping, the failure resolution order, and unmapped-failure `500`s |
| `skills/purview-results-zodsharp-validation` | ZodSharp validation flowing through results |
| `skills/purview-results-zodsharp-problems` | Rendering validation-carrying failures as ProblemDetails |

Task-shaped detail lives in a skill's own `references/` folder — migrating existing code onto error unions is
`skills/purview-results-union-errors/references/migrating-error-returns.md`.

## When to load which skill

- Modelling or migrating to result error unions → `purview-results-union-errors`
- The result type, its states or its combinators → `purview-results-core`
- Result-to-HTTP mapping or unmapped-failure `500`s → `purview-results-http-mapping`
- ZodSharp validation flowing through results → `purview-results-zodsharp-validation`, then
  `purview-results-zodsharp-problems`

## Upstream skills

`Purview.BuildSdk` and the source-generator framework repositories publish their own skills (project placement,
SDK configuration, generator authoring and testing, TUnit authoring) through the same CLI. Those are owned by
the repository that publishes them — changes belong there, not here. The CLI installs them into `.agents/skills/`,
which is gitignored; this repository's own skills are never installed into it, because two copies would drift
apart.

## Related

- [Contributing](Contributing.md) — where the authored content lives in this repository.
- [Source Generator](Source-Generator.md) — packaging and distribution of the component.
