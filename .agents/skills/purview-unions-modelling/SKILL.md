---
name: purview-unions-modelling
description: "Use when modelling a closed set of cases as a C# 15 union with Purview.Unions — choosing between the typeof list, partial factory signatures and an IUnionMembers provider, writing class or struct unions (including generics), or explaining why Value boxes but pattern matching does not."
---

# Modelling unions with Purview.Unions

Use this skill when a type is **exactly one of a fixed set of cases** and you want the compiler to
enforce that set, while value-type cases stay allocation-free.

## Why not the `union` keyword

A `union` declaration is a struct holding a single `object?`, so every value-type case is boxed on
entry and again on read. `Purview.Unions` generates the language's **custom union type** instead:

- `[System.Runtime.CompilerServices.Union]`
- one public constructor per case
- `public object? Value { get; }`
- `public bool HasValue { get; }`
- `public bool TryGetValue(out TCase value)` per case

The compiler then synthesises implicit conversions, checks `switch` exhaustiveness, and calls
`TryGetValue`/`HasValue` for patterns. `Value` still boxes when you read it — that is inherent to the
contract — but construction, `TryGetValue`, `is` and `switch` do not.

## Choose a declaration surface

### A — `typeof` list (non-generic)

```csharp
using Purview.Unions;

[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;
```

The generator emits the members and the `[Union]` attribute. Use this whenever every case can be
spelled in an attribute argument.

### B — partial factory signatures (generic-capable)

```csharp
[GenerateUnion]
public readonly partial struct Option<T>
{
    internal static partial Option<T> Some(Some<T> value);
    internal static partial Option<T> None(None value);
}
```

The case type is each method's single parameter. Use this when a case names a type parameter, because
`typeof(T)` in an attribute argument is `CS0416`.

### C — nested `IUnionMembers` provider (generic-capable)

```csharp
[GenerateUnion]
public readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
    public interface IUnionMembers
    {
        static Outcome<T> Create(T? value) => Outcome<T>.CreateCase0(value);
        static Outcome<T> Create(Exception? error) => Outcome<T>.CreateCase1(error);

        object? Value { get; }
        bool HasValue { get; }
        bool TryGetValue(out T value);
        bool TryGetValue(out Exception value);
    }
}
```

The language finds a union's members **only** on a public nested interface named `IUnionMembers` when
one exists. A source generator cannot add bodies to an interface you declared, so the provider owns its
one-line `Create` bodies and delegates to the generated `CreateCaseN` methods. Use this surface when you
want the case list visible to other generators.

Because `IUnionMembers` must be **public**, a strict analyzer set reports `CA1034` and `CA1515` (nested types
should not be visible; public types should be internal). The package's `UNI0000`/`UNI9001` suppression
descriptors handle both for an `IUnionMembers` provider nested in a union; no attribute is needed. The nested
shape is mandated by the language, not a design choice.

### D — hand-written `Purview.Unions.Union<T1, T2>`

```csharp
Union<int, string> value = 42;
if (value.TryGetValue(out int number)) { /* 42 */ }
```

No generator and no attributes. On `net10.0` it is an ordinary struct; on `net11.0` it also carries
`[Union]`, so it is a compiler union there too. Use it when you cannot run the generator or need exactly
two cases.

## Rules of thumb

- **One case type, one meaning.** Cases are the vocabulary of the domain; a case carrying a
  `string Reason` invites callers to match on the string instead of the type.
- **Keep unions small.** A dozen cases is usually several families sharing a name.
- **Prefer records for cases.** They give value equality and readable `ToString`; `Purview.BuildSdk`'s
  `PDS0006` suppressor already answers `CA1815` on the union itself.
- **Do not expect equality on a generated union.** v1 emits no `Equals`, `GetHashCode`, `ToString`,
  `Deconstruct`, `Match` or `Switch`; match the active case instead. A `record` union gets the compiler's
  record equality, which includes the hidden tag and storage fields.
- **A union cannot be named `Value`**, and it cannot already declare `Value`, `HasValue` or
  `TryGetValue`; the generated members would collide (`UNI0016`).

## Nullable value types

A nullable value-type case is normalised to its **underlying** type. `int?` becomes case `int`, the
constructor takes `int?`, and `TryGetValue(out int)` returns false for null — a null value leaves the
union empty (`Tag == 0`).

## What the compiler gives you

```csharp
IntOrString value = 42;                 // implicit union conversion

var text = value switch                 // exhaustive over int and string
{
    int number => $"int {number}",
    string message => $"string {message}",
    null => "empty",                    // struct unions can be empty
};

if (value is int number) { }            // TryGetValue, no boxing
if (value is null) { }                  // HasValue, no boxing
```

## Checklist

1. Every case type is at least as accessible as the union: public for a public union, internal is enough for
   an internal union.
2. The declaration is `partial` and is a `struct`, `readonly struct`, `class` or record.
3. A generic union uses surface B or C, never a `typeof` list.
4. No two cases are implicitly convertible to each other (`UNI0008`).
5. The build is clean of `UNI0001`–`UNI0016` and `UNI9000`.
