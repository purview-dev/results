---
agent: ask
description: "Migrate ad-hoc failure signalling (domain exceptions, bool/out pairs, error enums) in the selected files to a C# 15 union of case types with Purview.Results and generated AsFailure<TValue>() and AsFailure() helpers."
---

You are modernising error handling in this repository. Apply the `purview-results-union-errors` skill for union
modelling and the generated helpers, and `purview-results-core` for the result type's three states and the
throw-on-misuse contract.

## Inputs

- Target file(s): `${input:targetFiles:Path(s) to the files to migrate}`
- Union name: `${input:unionName:Name for the error union (for example TenantError)}`
- Failure modes: `${input:failureModes:Comma-separated failure modes to model as cases (leave blank to infer from the code)}`
- Keep exceptional throws: `${input:keepExceptions:true|false}` (default `true`)

## Task

For each target file, replace the current failure signalling for the identified operation family with a union of
case types and a `Result<TValue, TUnion>` return type built with the generated helpers.

### Requirements

1. Declare one `readonly record struct` per failure mode, carrying the payload a caller needs to react, and
   declare the union once:

   ```csharp
   [GenerateResult]
   public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
   ```

2. Change the affected signatures to `Result<TValue, TenantError>` — or the value-less `Result<TenantError>` when
   the member has nothing to return on success — and produce failures with `new Case(...).AsFailure<TValue>()` or
   `new Case(...).AsFailure()`.
3. Leave genuinely exceptional exits throwing when `keepExceptions` is true — a lost connection or a violated
   invariant is not a domain outcome.
4. Do **not** attempt implicit conversions from a case to a result, and do not add operators, extension
   conversions or wrapper types: `CS0715`, `CS0556`, `CS9282`, `CS0246` and `CS0029` each block that route. Use
   the generated helper, or `(TenantError)new TenantNotFound(id)` when a cast reads better.
5. Remove the replaced mechanism — thrown domain exceptions, error enums, `out` error parameters, sentinel
   values — rather than leaving both paths in place.
6. Keep the union free of transport concerns: no `IResult`, no status codes, no HTTP types.

### Migration strategy

- Inventory the failure modes and the call sites of each affected member.
- Introduce the cases and the union before changing signatures, so each edit compiles in isolation.
- Update call sites to `Match`/`Map`/`Bind` where a value has to come out of the result.
- Update or add tests: one per case, asserting the case the caller receives.
- Add the union to the host's result mapping (see the `purview-results-http-mapping` skill) if the code is
  reachable from an endpoint.

### Verification

- Build the solution and confirm no `RSG1000`–`RSG1007` diagnostics.
- Run the affected tests.
- Confirm no remaining `throw` for an outcome the caller is expected to handle.
- Confirm the union appears in exactly one mapper.

### Output format

Return:

1. Files changed
2. The union and its cases
3. The failure modes that stayed exceptional, and why
4. Verification performed
