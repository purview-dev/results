---
name: purview-results-zodsharp-problems
description: "Use when a Purview.Results failure that carries ZodSharp validation errors has to render as an ASP.NET Core HttpValidationProblemDetails — registering AddResultsZodSharpHttp, calling ToValidationProblem, or making a result-carried rejection and a thrown ZodException produce identical responses."
---

# Rendering validation-carrying failures as validation problems

Use this skill when a result failure carries ZodSharp `ValidationError` values (it implements
`IValidationErrorCarrier`) and the host has to respond with `application/problem+json`.

## Registration

```csharp
builder.Services.AddZodSharpProblemDetails();   // the ZodSharp package's own problem options/exception handler
builder.Services.AddResultsHttp();              // the result-to-response mapping
builder.Services.AddResultsZodSharpHttp();      // the bridge: validation-carrying failures -> validation problem
```

- `AddResultsZodSharpHttp()` registers the ZodSharp problem options (a no-op when the host already did) and adds
  the mapping as a **`ResultsHttpOptions.AddFallback`**.
- Because it is a fallback, **a mapping the host registered for a specific error case always wins**, and a
  mapping registered for the error type wins too. That is the intended escape hatch: when a case deserves its
  own response, register it.
- Pair it with `AddZodSharpProblemDetails()` so a thrown `ZodException` and a result-carried rejection are
  rendered by the same mapper and therefore produce identical responses.

## Rendering by hand

| Member | Purpose |
| --- | --- |
| `IValidationErrorCarrier.ToValidationProblem(context, statusCode = 400)` | Renders the errors the error value carries |
| `ImmutableArray<ValidationError>.ToValidationProblem(context, statusCode = 400)` | Renders a set of errors |
| `ZodValidationProblems.ToProblem(errors, options, defaultStatusCode = 400, traceId = null)` | The underlying mapper, for hosts that resolve the options themselves |

```csharp
if (result.Error is IValidationErrorCarrier carrier)
    return carrier.ToValidationProblem(context);
```

The response is written as `application/problem+json` through `TypedResults.Json`, so it does not depend on the
MVC problem-details writer.

## Status code and trace identifier

- The status code resolved by `ZodProblemDetailsOptions.StatusCodeSelector` wins.
- `statusCode` (or `defaultStatusCode`) is only used when the resolved error type does not define one — the
  default is `400 Bad Request`.
- The request trace identifier is included when `ResultsHttpOptions.IncludeTraceId` is `true` (the default).

## Pitfalls

- **Do not reimplement error-to-problem mapping.** Reuse `ZodValidationProblems.ToProblem` so a carried failure
  and a thrown `ZodException` cannot drift apart in status code, title, `errors` shape or `issues` extension.
- **Do not map the whole union to a validation problem** unless every case is a validation failure; map the
  carrying case (or rely on the fallback, which already does exactly that).
- If a case does deserve its own response, still build it from `ToValidationProblem(context)` when the payload
  is validation data, so clients see one shape.
- The fallback receives the *case* value, so the carrier is the union case that implements the interface — not
  the union itself.

## Verifying the pairing

A test that exercises both paths is the cheapest guard against drift:

1. return a failure carrying validation errors through an endpoint, and
2. throw the same errors as a `ZodException` through the host's exception handler,

then assert the two response bodies are equal. See the `purview-results-http-mapping` skill for the surrounding
registration and status-code guidance.
