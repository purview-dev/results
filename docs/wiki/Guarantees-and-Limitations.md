# Guarantees and Limitations

This page records what the suite guarantees, and what it deliberately does not do. Treat it as the contract a
change must not break.

## Runtime guarantees

- `Result<TValue, TError>` is a `readonly record struct` with exactly three observable states: `Uninitialized`
  (the `default` value), `Success` and `Failure`, each observable through `IsInitialized`, `IsSuccess` and
  `IsFailure`.
- `Result<TError>` is the value-less counterpart: a `readonly record struct` with the same three states and the
  same throw-on-misuse contract, whose success holds the `Success` marker instead of a value. `Error` throws in
  the wrong state, `Match`, `Map`, `Bind`, `MapError` and the extension operations throw
  `"The result is uninitialized."` for `default`, `Throw()` returns the `Success` marker or throws
  `ResultException<TError>`, and `ToString()` stays `Success` / `Failure(error)` / `Uninitialized`.
- The throw-on-misuse contract holds: `Value` and `Error` throw `InvalidOperationException` in the wrong state,
  and `Match`, `Map`, `Bind` and `MapError` throw `"The result is uninitialized."` for `default`. An uninitialized
  result is never silently coerced into a success or a failure.
- `Throw()` returns the successful value, throws `ResultException<TError>` carrying the error on failure, and
  throws `"The result is uninitialized."` for `default`. It is the one deliberate result-to-exception escape hatch.
- `ToString()` stays `Success(value)` / `Failure(error)` / `Uninitialized`.
- `IResultValue` accessors never throw; the accessor that does not describe the current state returns `null`. A
  successful unit result's `SuccessValue` is the `Success` marker.
- The implicit conversions from `TValue`, `TError` and `Success`, and the `Result.Success`/`Result.Failure`,
  `Result<TValue, TError>.Success`/`.Failure` and `Result<TError>.Success`/`.Failure` factories, are public
  contract.

## Observability

`Purview.Results.AspNetCore` emits metrics under the meter `Purview.Results.AspNetCore`
(`ResultsHttpTelemetry.MeterName`):

| Instrument | Meaning |
| --- | --- |
| `purview.results.failures.mapped` | failures a registered mapping or fallback answered |
| `purview.results.failures.unmapped` | failures with no mapping, answered with the unmapped-failure response |
| `purview.results.uninitialized` | endpoints that returned a `default` result, which is always a bug |

Each measurement carries a `purview.results.error_type` tag with the error's type name, and the failure
paths add the same tag to `Activity.Current` — enriching the request span ASP.NET Core already created
rather than starting one. The type name is always present in telemetry, because metrics and traces stay
inside your own infrastructure; disclosing it in the HTTP *response* is separately opt-in through
[`IncludeErrorTypeInProblemDetails`](#http-mapping-philosophy).

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(ResultsHttpTelemetry.MeterName));
```

Log messages carry stable event identifiers, so they can be filtered without matching message text:
`1000` uninitialized result, `1001` unmapped failure, `2000` a ZodSharp rule answering a multi-error
failure. No log statement records a validation payload or any user input.

These use `Meter` and `Activity` from the base class library, available through the ASP.NET Core framework
reference, so no telemetry package dependency is added.

## Thread safety

- **A result is safe to pass between threads by value.** It is a `readonly record struct` with no mutable
  state, so copying it — returning it, passing it as an argument, awaiting a `Task<Result<…>>` — is safe.
- **A result is _not_ safe to store in a shared mutable field and write concurrently.** A multi-field struct
  is not written atomically: `Result<TValue, TError>` holds the value, the error and a state byte, so a
  reader can observe a *torn* value where the state says `Success` while the value field is still the
  previous one.

  This is worse than it first looks, because of the nullability annotations. `IsSuccess` is annotated
  `[MemberNotNullWhen(true, nameof(Value))]`, so the compiler has been told a success implies a non-null
  `Value` — and will elide the null check at your call site. A torn read therefore surfaces as a
  `NullReferenceException` in your code, with nothing pointing back here.

  If you need to share a result through mutable state, synchronise it, or box it into a field of a reference
  type and publish that reference (a reference assignment *is* atomic):

  ```csharp
  // Not safe: concurrent writers can tear the struct.
  static Result<Tenant, TenantError> _cached;

  // Safe: the reference is published atomically.
  static Tuple<Result<Tenant, TenantError>>? _cached;
  ```

- `ResultsHttpOptions` is **startup configuration**. Its mapping dictionary and fallback list are frozen the
  first time a request reads them, so a registration added after the first failure is handled is ignored
  rather than corrupting a collection another thread is enumerating. Configure it in `AddResultsHttp` and do
  not mutate it afterwards.
- The generated helpers and factories are static and stateless, and `DefaultResultsHttpMapper` holds no
  per-request state, so both are safe to use concurrently.

## Dependency guarantees

- `Purview.Results` is dependency-free.
- The ZodSharp and ASP.NET Core packages depend on it, never the reverse.
- `Purview.Results.AspNetCore` deliberately knows nothing about ZodSharp; validation handling lives in
  `Purview.Results.ZodSharp.AspNetCore`.
- The runtime packages contain no reflection, no `dynamic` and no runtime type discovery. Union structure is
  inspected only by the source generator, through Roslyn symbols.

## Union support limits

- `[GenerateResult]` is supported on union declarations only. Generic unions are unsupported (`RSG1002`), and so
  are `IUnionMembers` member providers (`RSG1007`).
- Generated implicit conversions are not possible — see [Union Errors](Union-Errors.md) for the five compiler
  rules. The per-case `AsFailure<TValue>()` and `AsFailure()` helpers, the union-receiver
  `Union.Failure(...)`/`Union.Success(...)` factories and the IDE code fix are the ergonomics the language allows;
  the helper-free alternative is the `(TenantError)caseValue` cast.
- Accessibility never widens: a union or case type that is not visible produces an `internal` generated class
  (`RSG1003` covers the case where generated code could not reference a type at all).

## HTTP mapping philosophy

- An unmapped failure is a host mapping gap, not a domain outcome. It is answered with `UnmappedStatusCode`
  (`500`) and a `ProblemDetails` carrying the `errorType` extension, and it is logged.
- `ThrowOnUnmappedFailure` exists for development and keeps throwing `InvalidOperationException`.
- An uninitialized result (`default`) takes the unmapped path and is logged, because an endpoint returning
  `default` is a bug.
- `IResultsFailureMapper` is the way in for rules keyed by a **value** rather than a type, and it must not become
  a catch-all that answers every failure — that hides exactly the gaps the unmapped-failure response exists to
  expose.

## ZodSharp mapping philosophy

- Code rules are consulted before category rules, which are consulted before origin rules (each in registration
  order), then the default validation problem, so a rule can only narrow what the host already gets.
- A rule matches when **any** of the failure's errors carries its code, category or origin: a rule a schema can
  silently never reach is the kind of gap this suite surfaces rather than hides.
- A factory returns `null` to decline, and matching continues. Factories see the failure's whole error set.

## Not in scope

- No exception-to-result conversion: exceptions remain for exceptional circumstances. The reverse — turning a
  result failure into an exception — is available explicitly through `Throw()`.
- No runtime union inspection: matching an error case is ordinary C# pattern matching.
- No replacement for `Result<TValue, TError>` or `Result<TError>`: the generator only adds call-site ergonomics.
- No `Task`/`ValueTask`-specific async combinators beyond `MapAsync` and `BindAsync`. The async combinators take
  `Task<T>` only, carry no `CancellationToken` overloads, and await with `ConfigureAwait(false)`.
- **No serialization support.** A result is a control-flow type, not a DTO, so neither `Result<TValue, TError>`
  nor `Result<TError>` ships a `JsonConverter` and neither is designed to round-trip. Reflection-based
  `System.Text.Json` serialization **throws** `InvalidOperationException`, because it probes whichever of
  `Value`/`Error` does not describe the current state and those accessors throw by contract. Serialize the
  outcome you want instead: unwrap with `Match`, `TryGetValue`/`TryGetError`, or — over HTTP — let
  [`Purview.Results.AspNetCore`](AspNetCore-Integration.md) map the result to a response. `IResultValue` is the
  non-throwing view available to infrastructure that has to inspect a result generically.

## Related

- [Core Concepts](Core-Concepts.md) — the state contract in detail.
- [Diagnostics](Diagnostics.md) — the rules that enforce the union limits.
- [ASP.NET Core Integration](AspNetCore-Integration.md) — the mapping order described above.
