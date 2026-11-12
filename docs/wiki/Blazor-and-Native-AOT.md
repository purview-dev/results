# Blazor and Native AOT

All four packages are marked `IsAotCompatible` and build clean under the trim and AOT analyzers.
`Purview.Results` and `Purview.Results.ZodSharp` also set `PurviewBrowserSafe=true`, so the SDK's trim, AOT and
browser-unsupported-API checks run on them whenever a WebAssembly consumer references them.

- `Purview.Results` and `Purview.Results.ZodSharp` contain no reflection, no dynamic code and no runtime type
  discovery, and the generator's `AsFailure` helpers and union-receiver factories are plain constructor calls. A
  trimmed application, a Native AOT application and a Blazor WebAssembly client (trimmed, or with
  `RunAOTCompilation=true`) use them with no IL2xxx/IL3xxx warning.
- `Purview.Results.AspNetCore` and `Purview.Results.ZodSharp.AspNetCore` are server packages, and they work in a
  Native AOT ASP.NET Core host as well as in Blazor Server and any JIT host. The mapper hands a successful value to
  `TypedResults` through `IResultValue.AcceptSuccess`, so it binds `Ok<TValue>` (or `Json<TValue>` for another
  `SuccessStatusCode`) with the endpoint's real value type, never `Ok<object>`. A Native AOT host returns
  `result.ToHttpResult()` from its handlers and lists the value types in its `JsonSerializerContext` - see
  [A Native AOT minimal API](#a-native-aot-minimal-api).

`just smoke-packed-consumers` proves all of this against the packed packages rather than the repository's projects:
it publishes a Native AOT console, a Native AOT minimal API (with a `Purview.ZodSharp.Http` client), a Blazor WebAssembly app (trimmed and AOT-compiled, then
loaded in a headless browser), a Blazor Server app and a `net10.0` console from the `.nupkg` files alone, with
warnings as errors, runs each one, and scans every output for compiler-host assemblies.

Two runnable examples show the same integration from the other side:

- [`Examples.Blazor.Wasm`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Blazor.Wasm) runs the
  compile-time `[ZodSchema]` validator and the result pipeline entirely in the browser and is compiled with the
  stable async lowering, because Mono has no runtime-async support.
- [`Examples.Blazor.Server`](https://github.com/purview-dev/results/tree/main/src/src/Examples.Blazor.Server) runs
  the same page under interactive server rendering, where runtime async is enabled.

```bash
dotnet run --project src/src/Examples.Blazor.Wasm --urls http://localhost:5217
dotnet run --project src/src/Examples.Blazor.Server --urls http://localhost:5218
```

## Target frameworks

| Package | Target frameworks |
| --- | --- |
| `Purview.Results`, `.ZodSharp`, `.AspNetCore`, `.ZodSharp.AspNetCore` | `net10.0`, `net11.0` |
| `Purview.ZodSharp` 3.x | `net10.0`, `net11.0` |
| `Purview.ZodSharp.Blazor` 3.x (optional) | `net11.0` |
| `Purview.ZodSharp.Http` 3.x (optional) | `net10.0`, `net11.0` |

A `[GenerateResult]` union needs `net11.0` in the project that declares it, in a Blazor
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

## Calling the API from a client

`Purview.ZodSharp.Http` is the client half of the same contract: it validates a request body before it is sent, and
reads the validation problem `Purview.ZodSharp.AspNetCore` writes back into ZodSharp errors. Both fold into a result
the way a local `Validate` does, so a Blazor WebAssembly page renders a rejection it found itself and one the server
found with the same union case:

```csharp
// Validated locally first: an invalid body is never sent, and its errors become the case.
var sent = await http
    .PostAsJsonValidatedAsync("/api/tenants", registration, validator, AppJsonContext.Default.TenantRegistration, services)
    .ToResultAsync<HttpResponseMessage, TenantError>(errors => new TenantInputInvalid(errors));

// Rejected by the server anyway (a rule only the server can check): the problem reads back into the same case.
if (sent.TryGetValue(out var response) && await response.TryReadZodValidationProblemAsync() is { } problem)
    return new TenantInputInvalid(problem.ToValidationErrors()).AsFailure<Tenant>();
```

`ToResultAsync` and `ToUnitResultAsync` accept the `Task<ValidationResult<T>>` that `PostAsJsonValidatedAsync`,
`PutAsJsonValidatedAsync`, `PatchAsJsonValidatedAsync` and `HttpContent.ReadFromJsonAndValidateAsync` return, as
well as the `ValueTask` that `ValidateAsync` returns. `validator` is the generated `{TypeName}SchemaValidator` (an
`IZodSchemaValidator<T>` with a parameterless constructor; `AddZodSharp{Assembly}Schemas()` registers it in the
`IZodSchemaFactory`, not as a service). The package is trim-, Native
AOT- and Blazor WebAssembly-safe; pass the `JsonTypeInfo` from your own `JsonSerializerContext`.

## A Native AOT minimal API

Two rules make a minimal API Native AOT-clean:

- **Return `result.ToHttpResult()` from each handler, not the result.** Under `PublishAot`, ASP.NET Core's request
  delegate generator resolves JSON metadata for every handler's declared return type when it builds the endpoints. A
  `Result<...>` has none - it is never serialized, and System.Text.Json's source generator cannot describe a union
  error (`SYSLIB1227`) - so a handler declared to return the result fails every request at startup, before
  `WithResultsHttp()` can run. `ToHttpResult()` returns a `ResultHttpResult`, an `IResult` that the registered mapper
  maps when it executes, with exactly the responses `WithResultsHttp()` produces. It also describes the success
  response to OpenAPI.
- **List the request bodies and the successful values in a `JsonSerializerContext`** that the HTTP JSON options
  resolve from - the **value** type (`Tenant`), not `Result<Tenant, TenantError>`. The problem responses - the
  unmapped-failure and uninitialized `500`s, your own `TypedResults.Problem(...)` mappings, and the ZodSharp
  validation problem - resolve from ASP.NET Core's and Purview.ZodSharp.AspNetCore's own contexts.

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

builder.Services.AddZodSharpMyAppSchemas();
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddResultsHttp(options =>
    options.Map<TenantAlreadyExists>(error =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Tenant '{error.TenantId}' already exists.")));
builder.Services.AddResultsZodSharpHttp();

var app = builder.Build();

var tenants = app.MapGroup("/api/tenants");
tenants.MapPost("/", (TenantRegistration registration, TenantRegistrationService service) =>
    service.Register(registration).ToHttpResult());
tenants.MapDelete("/{tenantId}", (string tenantId, TenantRegistrationService service) =>
    service.Remove(tenantId).ToHttpResult()); // Result<TenantError>: 204

app.Run();

[JsonSerializable(typeof(TenantRegistration))]
[JsonSerializable(typeof(Tenant))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

Two more things keep the host itself AOT-clean:

- Set `ResultsHttpOptions` in code. Binding them from configuration with `Configure<ResultsHttpOptions>(section)`
  uses the reflection-based configuration binder unless you enable its source generator
  (`EnableConfigurationBindingGenerator`).
- A `SuccessMapper` receives the value as `object?`. Serializing that with `TypedResults.Json(value)` binds
  `Json<object>`, which has no metadata under Native AOT; cast to the value type first, or leave `SuccessMapper`
  unset and let the mapper keep the static type.

The packed-consumer gate's `scripts/packed-consumers/AotWebApi` is this host, published with `PublishAot` and
warnings as errors, then called - including a client self-test that goes through `Purview.ZodSharp.Http`.
## Value objects in a Blazor client

A `[Scalar]` from Purview.ValueObjects with a type-level `[ZodRule]` such as `[NonSentinel]` composes with results
in a Native AOT application and in a Blazor WebAssembly client, trimmed or published with `RunAOTCompilation=true`
(see [Value Objects Composition](Value-Objects-Composition.md)). Purview.ValueObjects' own guide covers binding a
value object in a form and where its validation messages are placed.
