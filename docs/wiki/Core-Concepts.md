# Core Concepts

`Result<TValue, TError>` is a `readonly record struct` with three states. Everything else in the suite — the
generator, the HTTP adapter, the ZodSharp bridge — is built on the behaviour described here.

## The three states

| State | How it is reached | `IsInitialized` | `IsSuccess` | `IsFailure` |
| --- | --- | --- | --- | --- |
| `Uninitialized` | `default(Result<TValue, TError>)` | `false` | `false` | `false` |
| `Success` | `Result<TValue, TError>.Success(value)` | `true` | `true` | `false` |
| `Failure` | `Result<TValue, TError>.Failure(error)` | `true` | `false` | `true` |

`IsSuccess` and `IsFailure` carry `[MemberNotNullWhen]`, so reading `Value` after an `IsSuccess` check flows the
nullability information the compiler needs.

## Throw on misuse, never coerce

An expected failure is a value; a misuse is a bug. The contract is deliberately loud:

- `Value` throws `InvalidOperationException` (`"The value of a non-successful result cannot be accessed."`) unless
  the result is a success.
- `Error` throws `InvalidOperationException` (`"The error of a non-failed result cannot be accessed."`) unless the
  result is a failure.
- `Match`, `Map`, `Bind`, `MapError` and the extension operations throw `InvalidOperationException`
  (`"The result is uninitialized."`) for `default`.

An uninitialized result is never treated as a failure or as a success. If that is too strict for a boundary —
inspecting a result you did not create — use the probing methods instead of the throwing ones.

`Throw()` is the one member that turns an expected failure back into an exception: it returns the successful value
and throws `ResultException<TError>` carrying the error on failure, and for `default` it throws the same
`InvalidOperationException` as the combinators. Treat it as an escape hatch for a boundary that must throw, not as
a way to handle a failure.

## Probing without throwing

`TryGetValue` and `TryGetError` in `ResultExtensions` are the deliberate exception to the throwing contract: they
report whether a value or an error is available and never throw, even for `default`.

```csharp
if (result.TryGetError(out var error))
    logger.LogWarning("Failed: {Error}", error);
```

## Factories and conversions

| Form | Example |
| --- | --- |
| Generic factory | `Result<Tenant, TenantError>.Success(tenant)` |
| Non-generic factory | `Result.Success<Tenant, TenantError>(tenant)` |
| Implicit from the value | `Result<int, string> ok = 42;` |
| Implicit from the error | `Result<int, string> failed = "not a number";` |

`Result.Success`/`Result.Failure` exist so a call site does not have to name the value type twice. The implicit
conversions from `TValue` and `TError` are public contract, and they are also what lets a method body `return`
a plain value or a non-union error.

## `ToString`

`ToString()` is stable and is what the examples print:

| State | Output |
| --- | --- |
| Success | `Success(value)` |
| Failure | `Failure(error)` |
| Uninitialized | `Uninitialized` |

## `IResultValue`

`IResultValue` is the non-generic, read-only view used by infrastructure that cannot be generic over the value and
error types — the ASP.NET Core endpoint filter, for example.

| Member | Behaviour |
| --- | --- |
| `IsInitialized` | Whether the result was created at all |
| `IsSuccess` | Whether the result represents success |
| `SuccessValue` | The successful value, or `null` when the result is not a success |
| `ErrorValue` | The error, or `null` when the result is not a failure |

Its accessors **never throw**: the accessor that does not describe the current state returns `null`. That is what
makes it safe for a framework component to inspect a result without knowing the two type arguments.

## Value-less results

An operation that has nothing to return on success — a command, a validation-only step — uses `Result<TError>`,
the value-less counterpart of `Result<TValue, TError>`. It has the same three states and the same throw-on-misuse
contract, but a success holds the `Success` marker rather than a value:

| State | How it is reached | `ToString()` |
| --- | --- | --- |
| `Uninitialized` | `default(Result<TError>)` | `Uninitialized` |
| `Success` | `Result<TError>.Success()` | `Success` |
| `Failure` | `Result<TError>.Failure(error)` | `Failure(error)` |

Because there is no success value, the success callbacks take no argument: `Match(() => ..., error => ...)`,
`Switch(() => ..., error => ...)` and `Tap(() => ...)`. `Map` produces a value from a success that had none,
`Bind` continues with another operation, and the value-result extension `DiscardValue()` turns a
`Result<TValue, TError>` back into a unit result, preserving the error. `Throw()` returns the `Success` marker
and throws `ResultException<TError>` on failure, exactly as the value result does.

The implicit conversions from `Success` and `TError`, and the `Result<TError>.Success`/`.Failure` and
`Result.Success<TError>()`/`Result.Failure<TError>(error)` factories, are public contract. `IResultValue` sees a
successful unit result's `SuccessValue` as the `Success` marker, which is what lets the ASP.NET Core mapper answer
it with `204 No Content`.

## Where extension methods live

Value-style operations that do not belong on the result type itself — probing, fallbacks, side effects,
constraints and the asynchronous combinators — live in
`src/src/Results/Extensions/Purview/Results/ResultExtensions.cs`. Add new extension methods there rather than on
`Result<TValue, TError>`, so the type's own surface stays the three states and the four core combinators.

## Related

- [Combinators](Combinators.md) — the operations built on these states.
- [Union Errors](Union-Errors.md) — making `TError` a union.
- [Guarantees and Limitations](Guarantees-and-Limitations.md) — the invariants that must not regress.
