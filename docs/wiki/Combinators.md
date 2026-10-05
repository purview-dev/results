# Combinators

The combinators fold a result into a value or chain another operation, without ever inspecting a state that is
not there. All of them throw `InvalidOperationException` (`"The result is uninitialized."`) for `default`; the
throwing input check for a `null` delegate is `ArgumentNullException`.

## Transforming

| Member | On success | On failure | On `default` |
| --- | --- | --- | --- |
| `Match(success, failure)` | `success(value)` | `failure(error)` | throws |
| `Map(map)` | `Result<TResult, TError>.Success(map(value))` | the existing error, unchanged | throws |
| `Bind(bind)` | the result `bind` returns | the existing error, unchanged | throws |
| `Bind(bind, mapError)` | the result `bind` returns | `Result<TResult, TNewError>.Failure(mapError(error))` | throws |
| `MapError(map)` | the value, unchanged | `Result<TValue, TNewError>.Failure(map(error))` | throws |

```csharp
Result<TenantName, TenantError> name = GetTenant(tenantId).Map(tenant => tenant.Name);

Result<Tenant, TenantError> created = GetTenant(tenantId)
    .Bind(tenant => store.CreateTenant(new TenantId("newco"), tenant.Name));

Result<Tenant, string> renamed = GetTenant(tenantId)
    .MapError(error => error.ToString());
```

`Map` and `Bind` preserve the error type; `MapError` preserves the value. `Bind` is the operation to reach for
when the next step can fail with the **same** error type — otherwise `MapError` first or a `Match` is clearer.

## Chaining across error types

`Bind` preserves the error type, so a step that fails with a **different** error needs the widening overload: it
maps the existing error into the next step's error type and binds in one call. It is equivalent to
`MapError(mapError).Bind(bind)`, and `BindAsync(bind, mapError)` is the asynchronous counterpart.

```csharp
Result<Tenant, RegisterTenantError> Register(TenantId id, string name) =>
    billing.ReserveQuota(id)                                  // Result<Quota, BillingError>
        .Bind(
            quota => CreateTenant(quota, name),               // Result<Tenant, RegisterTenantError>
            error => error);                                  // BillingError -> RegisterTenantError
```

The target union is the operation family's error, and the upstream service's error union is one of its cases, so
`error => error` relies on the implicit union conversion. When the target union flattens the services' leaf cases
instead, map them explicitly with `MapError`. [Union Errors](Union-Errors.md) covers the modelling choice and its
HTTP-mapping consequence.

## Constraining a success

`Ensure(predicate, errorFactory)` turns a successful value that fails the predicate into a failure:

```csharp
Result<Tenant, TenantError> enabled = GetTenant(tenantId)
    .Ensure(tenant => tenant.Enabled, tenant => new TenantDisabled(tenant.Id));
```

The `errorFactory` is invoked only when the predicate fails, so a satisfied constraint allocates no error. A
failure and its error pass through unchanged.

## Probing

`TryGetValue` and `TryGetError` never throw — not even for `default`:

```csharp
if (result.TryGetValue(out Tenant? tenant))
    Console.WriteLine(tenant.Name);

if (result.TryGetError(out TenantError error))
    Console.WriteLine(error);
```

## Fallbacks and alternatives

| Member | Purpose |
| --- | --- |
| `GetValueOrDefault()` | The successful value, or `default` |
| `GetValueOrDefault(TValue fallback)` | The successful value, or `fallback` |
| `GetValueOrDefault(Func<TValue> fallbackFactory)` | The successful value, or a lazily produced fallback |
| `GetErrorOrDefault(TError fallback)` | The error, or `fallback` |
| `OrElse(Result<TValue, TError> fallback)` | This result when successful, otherwise `fallback` |

Each of these still throws for `default`, because an uninitialized result is a bug rather than an ordinary
failure. Use them after a state has been established (or after probing).

## Side effects

| Member | Purpose |
| --- | --- |
| `Switch(Action<TValue> success, Action<TError> failure)` | Invoke the matching action and discard the value |
| `Tap(Action<TValue> success)` | Observe a success and return the result unchanged |
| `TapError(Action<TError> failure)` | Observe a failure and return the result unchanged |

`Tap` and `TapError` leave the result untouched, which keeps them usable in the middle of a chain.

## Asynchronous composition

```csharp
Task<Result<TenantDto, TenantError>> dto = GetTenantAsync(tenantId)
    .MapAsync(tenant => _mapper.MapAsync(tenant));

Task<Result<Tenant, TenantError>> saved = GetTenantAsync(tenantId)
    .BindAsync(tenant => _store.SaveAsync(tenant));
```

`MapAsync`, `BindAsync` and the widening `BindAsync(bind, mapError)` behave exactly like their synchronous
counterparts, and all throw for `default`. They are extension methods in `ResultExtensions`, not members on the
result type.

## Unwrapping with `Throw`

`Throw()` returns the successful value and throws a `ResultException<TError>` carrying the error when the result
represents failure:

```csharp
var tenant = store.GetTenant(tenantId).Throw();   // throws ResultException<TenantError> on failure
```

It is the deliberate result-to-exception escape hatch — reach for it at a boundary that must throw, or in a test,
not to handle an expected failure, which is normally a value. The thrown exception exposes the error with its
original type through `ResultException<TError>.Error`; a non-generic `ResultException` base carries the same error
as `object?` for a catch-all. An uninitialized result is a bug, so `Throw()` reports it with
`InvalidOperationException` (`"The result is uninitialized."`) rather than `ResultException<TError>`.

## Value-less results

`Result<TError>` supports the same operations, but its success callbacks take no argument because there is no
value to pass. `Match`, `Switch`, `Tap` and `Ensure` therefore use a no-argument success delegate:

```csharp
Result<TenantError> deleted = DeleteTenant(tenantId);

deleted.Match(() => "deleted", error => $"could not delete: {error}");
deleted.Ensure(() => _tenants.Count > 0, () => new TenantError());
```

`Map(() => value)` attaches a value to a success, producing a `Result<TResult, TError>`, and `Bind` continues with
either another unit operation or a value-producing one. The widening `Bind` and `BindAsync` overloads exist in
both shapes. `Result<TValue, TError>.DiscardValue()` is the inverse of `Map`: it drops the value and keeps the
error, so a value result can join a unit-result chain. `Throw()` returns the `Success` marker and throws
`ResultException<TError>` on failure, and `default` still throws `InvalidOperationException`.

## Related

- [Core Concepts](Core-Concepts.md) — the state contract these operations obey.
- [Union Errors](Union-Errors.md) — producing the failure value a combinator carries.
