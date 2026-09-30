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

`MapAsync` and `BindAsync` behave exactly like their synchronous counterparts, and both throw for `default`.
They are extension methods in `ResultExtensions`, not members on the result type.

## Related

- [Core Concepts](Core-Concepts.md) — the state contract these operations obey.
- [Union Errors](Union-Errors.md) — producing the failure value a combinator carries.
