# Purview.Results Wiki

Purview.Results is the Purview result suite for .NET: a small, dependency-light `Result<TValue, TError>` value
that makes **expected** failures part of a method's contract instead of an exception, a Roslyn source generator
that makes C# 15 union error cases ergonomic, and adapters that carry those results to ASP.NET Core and
ZodSharp. Exceptional circumstances still throw; states you expect — not found, invalid, conflict — are values.

This wiki is the project documentation hub. Packages are published under the `Purview.Results.*` package IDs.

## Start here

- [Getting Started](Getting-Started.md)
- [Core Concepts](Core-Concepts.md)
- [Combinators](Combinators.md)
- [Union Errors](Union-Errors.md)
- [Guarantees and Limitations](Guarantees-and-Limitations.md)

## Source generation

- [Source Generator](Source-Generator.md)
- [Diagnostics](Diagnostics.md)

## Integrations

- [ASP.NET Core Integration](AspNetCore-Integration.md)
- [ZodSharp Integration](ZodSharp-Integration.md)
- [ZodSharp Problem Details](ZodSharp-ProblemDetails.md)

## Workflow

- [Testing](Testing.md)
- [Agent Skills](Agent-Skills.md)
- [Release Flow](Release-Flow.md)
- [Contributing](Contributing.md)

## Packages

| Package | Purpose |
| --- | --- |
| `Purview.Results` | `Result<TValue, TError>`, the `Result` factories and `IResultValue`, plus the bundled source generator for `[GenerateResult]` unions. No runtime dependencies. |
| `Purview.Results.AspNetCore` | Maps results onto ASP.NET Core responses (`IResult`, `ProblemDetails`). |
| `Purview.Results.ZodSharp` | Bridges ZodSharp `ValidationResult<T>` values into results. |
| `Purview.Results.ZodSharp.AspNetCore` | Renders validation-carrying failures as `HttpValidationProblemDetails`. |

## Feature highlights

- **Three explicit states** — `Uninitialized` (the `default` value), `Success` and `Failure` stay observable, so
  a result that was never created is never silently read as a failure or a success.
- **Throw-on-misuse, never silent** — `Value` and `Error` throw in the wrong state, and `Match`/`Map`/`Bind`/
  `MapError` throw for `default`; probing (`TryGetValue`, `TryGetError`) is the non-throwing way in.
- **Union error types** — a C# 15 union keeps every error case strongly typed while the result stays a single
  value, and the generator supplies the per-case `AsFailure<TValue>()` helper the language cannot express itself.
- **Compile-time diagnostics** — `RSG1000`–`RSG1007` report unsupported union shapes at build time, and the one
  suppression (`RSG2000`) answers `CA1815` only for opted-in unions.
- **Host-controlled HTTP** — the ASP.NET Core package resolves an error **case**, then the **error type**, then
  ordered fallbacks, and answers an unmapped failure with a logged `500` so mapping gaps stay visible.
- **Validation without exceptions** — ZodSharp `ValidationResult<T>` values become ordinary results, and a
  failure that carries validation errors becomes a standard ProblemDetails payload.
- **No reflection at runtime** — dependency-free runtime packages; union structure is inspected only by the
  source generator, through Roslyn symbols.
