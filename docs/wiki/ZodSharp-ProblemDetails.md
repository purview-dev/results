# ZodSharp Problem Details

`Purview.Results.ZodSharp.AspNetCore` renders a result failure that carries ZodSharp validation errors as an
ASP.NET Core `HttpValidationProblemDetails`, produced by the same mapper the ZodSharp exception handler uses — so a
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

// Every validation-carrying failure becomes one validation problem, except the codes, categories and origins
// that a rule answers with something else.
builder.Services.AddResultsZodSharpHttp(options => options
    .MapCode("tenant_not_found", StatusCodes.Status404NotFound)
    .MapCategory("invalid_value", StatusCodes.Status422UnprocessableEntity)
);

var app = builder.Build();

app.MapPost("/reconcile", (ReconciliationRequest request) => Reconcile(request)).WithResultsHttp();
```

`AddResultsZodSharpHttp` registers the mapping as a **failure mapper**, so:

- a mapping the host registered for a specific error **case** always wins,
- a mapping registered for the **error** type wins,
- and a host failure mapper registered **before** `AddResultsZodSharpHttp` wins too.

Register it **last**, therefore, so everything the host declared earlier takes precedence.

## Answering by validation error code, category or origin

| Member | Purpose |
| --- | --- |
| `MapCode(string code, int statusCode)` | Renders the validation problem with a different default status |
| `MapCode(string code, Func<ImmutableArray<ValidationError>, HttpContext, IResult?>)` | Renders a response of your own |
| `MapCategory(string category, int statusCode)` | The same, for a category that spans many codes |
| `MapCategory(string category, Func<ImmutableArray<ValidationError>, HttpContext, IResult?>)` | Renders a response of your own |
| `MapOrigin(string origin, int statusCode)` | The same, for an origin a rule owns (`"value_object"`, `"array"`, …) |
| `MapOrigin(string origin, Func<ImmutableArray<ValidationError>, HttpContext, IResult?>)` | Renders a response of your own |

The three axes are the identity a `ValidationError` carries: **code** is the specific rule that failed,
**category** is a broad grouping many codes share, and **origin** is the structured origin a rule owns. Use
`MapOrigin` when the answer is keyed by the *kind* of rule rather than one code — for example rendering every
type-level rule failure, which reports `"value_object"`, as `422` whatever its code.

A rule applies when **any** of the failure's errors carries its code, category or origin, so a registered rule is
always reachable whatever else the schema reported. Matching walks the **code** rules first, then the **category**
rules, then the **origin** rules, each in registration order, and finally the default validation problem — a code is
narrower than a category, which is narrower than an origin, so a code wins however the rules were registered. A
factory that wants stricter semantics returns `null` to decline the failure, and matching continues:

```csharp
options
    // A code rule that only answers a failure whose every error is that code.
    .MapCode("tenant_not_found", (errors, context) =>
        errors.All(error => error.Code == "tenant_not_found")
            ? TypedResults.NotFound()
            : null)
    // A category rule that answers the failures the code rule declined.
    .MapCategory("invalid_value", StatusCodes.Status422UnprocessableEntity)
    // An origin rule: every type-level rule failure, whatever its code.
    .MapOrigin("value_object", StatusCodes.Status422UnprocessableEntity);
```

A factory receives the failure's **full error set**, so a rule never hides the other problems the caller has to
fix, and the `int statusCode` overloads still render every error as an `HttpValidationProblemDetails`. A code,
category or origin registered twice with different behaviour is rejected at configuration time.

A failure that does **not** carry validation errors is declined (`null`), so other mappers and fallbacks still
apply — this mapper never answers a failure it does not understand.

## API

| Member | Purpose |
| --- | --- |
| `AddResultsZodSharpHttp(Action<ZodResultsHttpOptions>? configure = null)` | Registers the ZodSharp options and adds the validation failure mapper to `ResultsHttpOptions` |
| `ZodResultsHttpOptions.MapCode` / `.MapCategory` / `.MapOrigin` | The per-code, per-category and per-origin rules described above |
| `ZodResultsFailureMapper` | The mapper itself, for a host that wants to register or compose it by hand |
| `IValidationErrorCarrier.ToValidationProblem(HttpContext, int statusCode = 400)` | Creates the validation problem for the errors the error value carries |
| `ImmutableArray<ValidationError>.ToValidationProblem(HttpContext, int statusCode = 400)` | Creates the validation problem for a set of errors |
| `ZodValidationProblems.ToProblem(errors, options, defaultStatusCode = 400, traceId = null)` | The underlying mapper, for hosts that resolve the options themselves |

The status code resolved by `ZodProblemDetailsOptions.StatusCodeSelector` wins; `statusCode` (or
`defaultStatusCode`, or a rule's `statusCode`) is only used when the resolved error type does not define one. The
trace identifier is included when `ResultsHttpOptions.IncludeTraceId` is `true` (the default).

The mapper reuses `ZodValidationProblems.ToProblem` rather than reimplementing error-to-problem mapping, which is
what keeps a result-carried validation failure and a thrown `ZodException` in agreement on status code, title,
detail and the `issues` extension.

## Example

```bash
dotnet run --project src/src/Examples.AspNetCore.Zod --urls http://localhost:5216
```

`Examples.AspNetCore.Zod` shows a `TenantInputInvalid` failure carrying ZodSharp errors becoming a `400`
validation problem, while the host's own `TenantAlreadyExists` mapping still returns `409`.

## Related

- [ZodSharp Integration](ZodSharp-Integration.md) — producing the results that carry validation errors.
- [ASP.NET Core Integration](AspNetCore-Integration.md) — the failure resolution order this mapper plugs into.
