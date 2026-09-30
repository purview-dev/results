# Purview.Results.ZodSharp.AspNetCore

[![NuGet version](https://img.shields.io/nuget/v/Purview.Results.ZodSharp.AspNetCore.svg)](https://www.nuget.org/packages/Purview.Results.ZodSharp.AspNetCore)
[![Release](https://github.com/purview-dev/results/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/results/actions/workflows/release.yml)

Renders a result failure that carries ZodSharp validation errors as an ASP.NET Core
`HttpValidationProblemDetails` response, produced by the same mapper the ZodSharp exception handler uses — so a
validation failure carried by a result and the same failure thrown as a `ZodException` produce identical
responses.

## Installation

```bash
dotnet add package Purview.Results.ZodSharp.AspNetCore
```

## Quick start

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZodSharpProblemDetails();
builder.Services.AddResultsHttp();
builder.Services.AddResultsZodSharpHttp();

var app = builder.Build();

app.MapPost("/reconcile", (ReconciliationRequest request) => Reconcile(request)).WithResultsHttp();
```

`AddResultsZodSharpHttp` registers the mapping as a **fallback**, so a mapping the host registered for a
specific error case always wins, and a mapping registered for the error type still wins over it.

## API

| Member | Purpose |
| --- | --- |
| `AddResultsZodSharpHttp()` | Registers the ZodSharp options and adds the validation-problem fallback to `ResultsHttpOptions` |
| `IValidationErrorCarrier.ToValidationProblem(HttpContext, int statusCode = 400)` | Creates the validation problem for the errors the error value carries |
| `ImmutableArray<ValidationError>.ToValidationProblem(HttpContext, int statusCode = 400)` | Creates the validation problem for a set of errors |
| `ZodValidationProblems.ToProblem(errors, options, defaultStatusCode = 400, traceId = null)` | The underlying mapper, for hosts that resolve the options themselves |

The status code resolved by `ZodProblemDetailsOptions.StatusCodeSelector` wins; `statusCode` (or
`defaultStatusCode`) is only used when the resolved error type does not define one. The trace identifier is
included when `ResultsHttpOptions.IncludeTraceId` is `true` (the default).

## Related packages

| Package | Purpose |
| --- | --- |
| [`Purview.Results.ZodSharp`](https://www.nuget.org/packages/Purview.Results.ZodSharp) | Produces the results that carry validation errors |
| [`Purview.Results.AspNetCore`](https://www.nuget.org/packages/Purview.Results.AspNetCore) | The result-to-response mapping this package extends |

## Agent skills

This package ships the `purview-results-zodsharp-problems` agent skill under `.agents/`. Repositories that import
`Purview.BuildSdk` get it mirrored into their own `.agents/` folder on the next restore or build, so AI agents
working there receive the guidance automatically.

## License

MIT — see [LICENSE.md](https://github.com/purview-dev/results/blob/main/LICENSE.md).
