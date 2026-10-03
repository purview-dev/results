# Guarantees and Limitations

This page records what the suite guarantees, and what it deliberately does not do. Treat it as the contract a
change must not break.

## Runtime guarantees

- `Result<TValue, TError>` is a `readonly record struct` with exactly three observable states: `Uninitialized`
  (the `default` value), `Success` and `Failure`, each observable through `IsInitialized`, `IsSuccess` and
  `IsFailure`.
- The throw-on-misuse contract holds: `Value` and `Error` throw `InvalidOperationException` in the wrong state,
  and `Match`, `Map`, `Bind` and `MapError` throw `"The result is uninitialized."` for `default`. An uninitialized
  result is never silently coerced into a success or a failure.
- `ToString()` stays `Success(value)` / `Failure(error)` / `Uninitialized`.
- `IResultValue` accessors never throw; the accessor that does not describe the current state returns `null`.
- The implicit conversions from `TValue` and `TError`, and the `Result.Success`/`Result.Failure` and
  `Result<TValue, TError>.Success`/`.Failure` factories, are public contract.

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
  rules. The per-case `AsFailure<TValue>()` helper and the IDE code fix are the ergonomics the language allows;
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

- No exception-to-result conversion: exceptions remain for exceptional circumstances.
- No runtime union inspection: matching an error case is ordinary C# pattern matching.
- No replacement for `Result<TValue, TError>`: the generator only adds call-site ergonomics.
- No `Task`/`ValueTask`-specific async combinators beyond `MapAsync` and `BindAsync`.

## Related

- [Core Concepts](Core-Concepts.md) — the state contract in detail.
- [Diagnostics](Diagnostics.md) — the rules that enforce the union limits.
- [ASP.NET Core Integration](AspNetCore-Integration.md) — the mapping order described above.
