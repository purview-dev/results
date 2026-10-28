# Blazor and Native AOT

`Purview.Results` and `Purview.Results.ZodSharp` contain no reflection, no dynamic code and no runtime type
discovery, and the generator's `AsFailure` helpers and union-receiver factories are plain constructor calls. A
trimmed application, a Native AOT application and a Blazor WebAssembly client (trimmed, or with
`RunAOTCompilation=true`) use them with no IL2xxx/IL3xxx warning.

`Purview.Results.AspNetCore` and `Purview.Results.ZodSharp.AspNetCore` are server packages. They are not marked
AOT-compatible: the HTTP mapper serializes a success through `IResultValue.SuccessValue`, typed `object?`, which has
no resolvable `JsonTypeInfo` under Native AOT. Blazor Server and any non-AOT ASP.NET Core host use them as usual.

`just smoke-packed-consumers` proves all of this against the packed packages rather than the repository's projects:
it publishes a Native AOT console, a Blazor WebAssembly app (trimmed and AOT-compiled, then loaded in a headless
browser), a Blazor Server app and a `net10.0` console from the `.nupkg` files alone, with warnings as errors, and
scans every output for compiler-host assemblies.

## Target frameworks

| Package | Target frameworks |
| --- | --- |
| `Purview.Results`, `.ZodSharp`, `.AspNetCore`, `.ZodSharp.AspNetCore` | `net10.0`, `net11.0` |
| `Purview.ZodSharp` 3.x | `net10.0`, `net11.0` |
| `Purview.ZodSharp.Blazor` 3.x (optional) | `net11.0` |

A `[GenerateResult]` union needs `net11.0` and `LangVersion=preview` in the project that declares it, in a Blazor
client as anywhere else.

## A Blazor form whose input flows through a result

Validate the form with `Purview.ZodSharp.Blazor`'s `<ZodValidator>`, and send the same input through the result
pipeline when it is submitted - a rejected input and a domain failure are both values the page can render:

```csharp
// Program.cs (WebAssembly client or server)
builder.Services.AddZodSharpMyAppSchemas(); // generated: AddZodSharp{Assembly}Schemas()
builder.Services.AddZodSharpBlazor();
```

```razor
<EditForm Model="registration" OnValidSubmit="Submit">
    <ZodValidator />
    <InputText @bind-Value="registration.TenantId" />
    <ZodValidationMessage For="() => registration.TenantId" />
    <button type="submit">Register</button>
</EditForm>

@code {
    readonly TenantRegistration registration = new();
    string? outcome;

    void Submit() =>
        outcome = service
            .Register(registration)   // Validate(...).ToResult<..., TenantError>(...).Bind(...)
            .Match(
                tenant => $"Registered {tenant.TenantId}.",
                error => error switch
                {
                    TenantAlreadyExists exists => $"Tenant {exists.TenantId} already exists.",
                    TenantInputInvalid invalid => $"Rejected with {invalid.Errors.Length} error(s).",
                    _ => "Failed.",
                });
}
```

On a Blazor Server or Web App host, the same service behind a minimal API maps onto HTTP with `WithResultsHttp()`: a
`TenantInputInvalid` (an `IValidationErrorCarrier`) becomes the ZodSharp validation problem, a code rule from
`AddResultsZodSharpHttp` answers its code, and a case mapping from `AddResultsHttp` answers its case.
`AddZodSharpBlazor()` validates form models only - it does not turn on .NET 11 minimal-API parameter validation - so
the handler runs and the result rules answer. See
[ZodSharp Problem Details](ZodSharp-ProblemDetails.md#net-11-request-validation).

## Value objects in a Blazor client

A `[Scalar]` from Purview.ValueObjects with a type-level `[ZodRule]` such as `[NonSentinel]` composes with results
in a Native AOT application and in a Blazor WebAssembly client, trimmed or published with `RunAOTCompilation=true`
(see [Value Objects Composition](Value-Objects-Composition.md)). Purview.ValueObjects' own guide covers binding a
value object in a form and where its validation messages are placed.
