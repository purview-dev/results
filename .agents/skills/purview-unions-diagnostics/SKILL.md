---
name: purview-unions-diagnostics
description: "Use when a Purview.Unions build reports a UNI diagnostic (UNI0001 to UNI0016 or UNI9000), when a generated union is missing its members, when a union conversion or exhaustive switch does not compile, or when deciding whether a finding is a design error or a code fix."
---

# Purview.Unions diagnostics

The package ships one analyzer and one generator. The analyzer reports every per-target rule; the
generator uses the same findings to decide whether it can emit, and reports only `UNI9000`. A rule is
never reported twice.

| ID | Severity | Meaning | Fix |
| --- | --- | --- | --- |
| `UNI0001` | Error | The union declaration is not `partial` | Add `partial` (code fix) |
| `UNI0002` | Error | Unsupported declaration kind (interface, enum, static/abstract class, `ref struct`) | Use a struct, readonly struct, class or record |
| `UNI0003` | Error | No case list was found | Add a `typeof` list, partial factories, a provider, or public constructors |
| `UNI0004` | Error | The same case type appears twice | Remove one; `int?` and `int` are the same case |
| `UNI0005` | Error | A case type is less accessible than the union | Make the case type at least as accessible as the union (code fix adds `public`) |
| `UNI0006` | Error | More cases than a `ushort` tag can address | Split the union |
| `UNI0007` | Error | `[Union]` is required but missing | Add it (code fix); only on the documented fallback path |
| `UNI0008` | Error | Two case types are implicitly convertible | Replace one, or wrap it in a distinct record |
| `UNI0009` | Error | A case cannot be a union case (pointer, function pointer, `ref struct`, `void`, static class, open generic, `dynamic`) | Use a storable type |
| `UNI0010` | Error | A nullable value-type case has no usable underlying form | Use a value type with a concrete underlying type |
| `UNI0011` | Error | A generic union uses a `typeof` case list | Use partial factories or a provider; `typeof(T)` is `CS0416` |
| `UNI0012` | Error | Existing constructors disagree with the declared list | Keep one source of cases, or make them agree |
| `UNI0013` | Error | A containing type cannot be named from generated code | Make every containing type non-generic and `partial` |
| `UNI0014` | Error | Two case lists disagree | Keep exactly one of the four sources |
| `UNI0015` | Warning | The union has one case | Use the case type directly |
| `UNI0016` | Error | The declaration collides with a required member | Rename the type, or remove the `Value`/`HasValue`/`TryGetValue` member |
| `UNI9000` | Error | The generator failed unexpectedly | Report it with the union name and message |

## Blocking behaviour

Every error blocks its union: the generated constructors, storage and access members are one coherent
shape, so there is no partial output to emit. `UNI0015` is the only warning and does not block.

`UNI0000` and `UNI9001` are **suppression descriptors**, not reported rules. `UNI0000` suppresses `CA1034`
(nested types should not be visible) and `UNI9001` suppresses `CA1515` (public types should be internal) on a
union's public nested `IUnionMembers` provider, which the C# union contract requires. Add either id to
`NoWarn` to turn that suppression off.

## `UNI0001` — not partial

The generator adds a second partial declaration. Without `partial` it cannot:

```csharp
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;   // add partial
```

## `UNI0003` / `UNI0011` — no usable case list

For a non-generic union, the attribute list is the simplest source:

```csharp
[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;
```

For a generic union, a `typeof` list cannot name `T`. Use partial factories (surface B) or a nested
`IUnionMembers` provider (surface C), as described in the `purview-unions-modelling` skill.

## `UNI0005` — a case is less accessible than the union

A case type must be at least as accessible as the union. A public union's creation members are public API,
so its case types must be public all the way up their containers. An internal union's creation members are
effectively internal, so an internal case type is enough. The code fix adds `public` to the case type and its
containing types.

## `UNI0008` — creation is ambiguous

Two case types with an implicit conversion between them make a value convertible to both, and the
compiler requires a single best creation member:

```csharp
[GenerateUnion(typeof(int), typeof(long))]   // UNI0008
public readonly partial struct IntOrLong;
```

Wrap one case in a distinct record (`record struct Count(int Value)`) so the conversion sets are disjoint.

## `UNI0012` / `UNI0014` — conflicting case lists

Cases are resolved in this order: the `typeof` list, partial factories, a nested provider, existing
public single-parameter constructors.

- A second, **non-constructor** list that differs is `UNI0014`.
- Existing constructors that differ from a declared list are `UNI0012`.
- When they agree, the author's constructors win and the generator emits only the attribute and access
  members it can see are needed — it never duplicates the constructors.

## `UNI0016` — a required member collides

The generated shape owns `Value`, `HasValue` and `TryGetValue`. A union named `Value` cannot have a
`Value` property (`CS0542`), and a source-declared member with one of those names would be duplicated.
Rename the type or the member.

## `UNI9000` — unexpected generator failure

The generator catches its own failures so one malformed union cannot discard every other union's output
(`CS8785`). The message names the union and the exception. Please report it with the declaration.

## Fixing the code, not the warning

There is deliberately no `NoWarn`/`#pragma` in this stack. The suppressions are `DiagnosticSuppressors`, not
reported rules: `Purview.BuildSdk`'s `PDS0006` for `CA1815` on a union declaration, and this package's
`UNI0000` for `CA1034` and `UNI9001` for `CA1515` on a union's nested `IUnionMembers` provider.

## Checklist

1. Read the id from the build output or the IDE.
2. Fix the declaration, not the diagnostic.
3. Rebuild; a union that was blocked has no generated members until the blocking rule is cleared.
