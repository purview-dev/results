# Agent Skills

The packages ship **agent skills** to consumers. A repository that imports `Purview.BuildSdk` receives them in
its own `.agents/` folder on the next restore or build, so AI agents working there get the guidance
automatically. The content is authored in this repository under `src/src/<Project>/Sdk/.agents/**` and packed as
`.agents/**`; there is nothing to install by hand.

## Skills shipped by this repository

| Content | Package | Covers |
| --- | --- | --- |
| `skills/purview-results-core` | `Purview.Results` | The result type, its three states, combinators and the throw-on-misuse contract |
| `skills/purview-results-union-errors` | `Purview.Results.SourceGenerator` | Union modelling, the generated helpers, the diagnostics table, and the language rules that make the helper necessary |
| `skills/purview-results-http-mapping` | `Purview.Results.AspNetCore` | Result-to-HTTP mapping, the failure resolution order, and unmapped-failure `500`s |
| `skills/purview-results-zodsharp-validation` | `Purview.Results.ZodSharp` | ZodSharp validation flowing through results |
| `skills/purview-results-zodsharp-problems` | `Purview.Results.ZodSharp.AspNetCore` | Rendering validation-carrying failures as ProblemDetails |

The generator package also ships the `purview-results-union-author` agent and the
`migrate-error-returns-to-result-unions` prompt.

## When to load which skill

- Modelling or migrating to result error unions → `purview-results-union-errors`
- The result type, its states or its combinators → `purview-results-core`
- Result-to-HTTP mapping or unmapped-failure `500`s → `purview-results-http-mapping`
- ZodSharp validation flowing through results → `purview-results-zodsharp-validation`, then
  `purview-results-zodsharp-problems`

## Upstream skills

`Purview.BuildSdk` and the source-generator framework packages also deliver their own skills (project placement,
SDK configuration, generator authoring and testing, TUnit authoring) into the same `.agents/` tree. Those are
owned by the package that ships them — changes belong in the owning repository, not here.

## Related

- [Contributing](Contributing.md) — where the authored content lives in this repository.
- [Source Generator](Source-Generator.md) — packaging and distribution of the component.
