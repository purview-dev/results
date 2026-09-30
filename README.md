# results

Purview result types for .NET — a small, dependency-light `Result<TValue, TError>` type, C# 15 union
ergonomics for its error cases, and integrations that let expected failures flow through a value instead of
an exception.

| Package | Purpose |
|---|---|
| `Purview.Results` | `Result<TValue, TError>` and the `Result` factories. No dependencies. |
| `Purview.Results.SourceGenerator` | Generates `AsFailure<TValue>()` helpers for `[GenerateResult]` unions. |
| `Purview.Results.ZodSharp` | Bridges ZodSharp `ValidationResult<T>` values into results. |
| `Purview.Results.AspNetCore` | Maps results onto ASP.NET Core responses (`IResult`, `ProblemDetails`). |
| `Purview.Results.ZodSharp.AspNetCore` | Maps result failures that carry validation errors onto `HttpValidationProblemDetails`. |
