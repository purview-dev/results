---
name: purview-unions-quickstart
description: "Use when adding Purview.Unions to a .NET project for the first time — installing the package, compiling with C# 15, declaring a first [GenerateUnion] union, matching it with an exhaustive switch, or falling back to the hand-written Union<T1, T2> without a generator."
---

# Purview.Unions quickstart

Use this skill when a project needs a closed set of cases and you want value-type cases to stay
allocation-free. A `union` declaration always stores one `object?`, so it boxes every value-type case;
`Purview.Unions` generates the language's **custom union type** instead.

## Install

```bash
dotnet add package Purview.Unions
```

One `PackageReference` is enough: the runtime, the merged source generator and its code fixes all ship in
the same package under `analyzers/dotnet/cs`.

## Requirements

- A **C# 15 compiler**: the .NET 11 SDK (or later). C# 15 is GA, so `LangVersion=preview` is not required.
- Target `net10.0` or `net11.0`. A `net11.0` project gets C# 15 by default and needs no `LangVersion`; a
  `net10.0` project sets `<LangVersion>latest</LangVersion>` because its default is C# 14.
- Do **not** set `EnablePreviewFeatures`. It emits `RequiresPreviewFeatures` assembly-wide and makes
  `CA2252` fire on every member a consumer touches; `LangVersion=latest` (only on `net10.0`) is the only
  switch needed.

On `net10.0` the generator emits internal `[Embedded]` polyfills for `UnionAttribute`/`IUnion`. On
`net11.0` the BCL types are used and no polyfill is emitted.

## First union

```csharp
using Purview.Unions;

[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;
```

The declaration must be `partial`. The generator adds:

- the `[System.Runtime.CompilerServices.Union]` attribute and the union interface;
- one typed field per case and a tag;
- one public constructor per case;
- `public object? Value { get; }` and `public bool HasValue { get; }`;
- `public bool TryGetValue(out TCase value)` per case.

## Use it

```csharp
IntOrString value = 42;                 // implicit union conversion

var text = value switch
{
    int number => $"int {number}",
    string message => $"string {message}",
    null => "empty",                    // a struct union can be empty
};

if (value is int number) { }            // TryGetValue, no boxing
if (value is null) { }                  // HasValue, no boxing
```

`switch` is exhaustive over the case types. `Value` still boxes a value-type case when you read it — that
is inherent to the language contract — but construction, `TryGetValue`, `is` and `switch` do not.

## Generic unions

`typeof(T)` is not a legal attribute argument (`CS0416`), so a generic union names its cases in source.
Either declare partial factory signatures:

```csharp
[GenerateUnion]
public readonly partial struct Option<T>
{
    internal static partial Option<T> Some(Some<T> value);
    internal static partial Option<T> None(None value);
}
```

or implement a nested `IUnionMembers` provider when another generator must see the case list. The
`purview-unions-modelling` skill explains when each surface is right.

## Without the generator

For exactly two cases, or a project that cannot run the generator:

```csharp
Purview.Unions.Union<int, string> value = 42;

if (value.TryGetValue(out int number))
    Console.WriteLine(number);          // 42

var text = value.Match(n => $"int {n}", s => $"string {s}");
```

`Union<T1, T2>` has `Value`, `HasValue`, `Tag`, `TryGetValue`, `Match`, `Switch`, equality and `ToString`.
On `net10.0` it is an ordinary struct; on `net11.0` it also carries `[Union]`, so it is a compiler union
there too.

## Turn the generator off

`DisableUnionsSourceGenerator=true` (MSBuild property) disables generation and the polyfills. It is useful
for inspecting the declared source in isolation; it is not a way to make a failing build pass.

## Choose a surface

| Surface | Shape | Use when |
| --- | --- | --- |
| A | `[GenerateUnion(typeof(int), typeof(string))]` | Every case can be named in an attribute argument |
| B | partial static factory signatures | A case names a type parameter |
| C | a nested `IUnionMembers` provider | A generic union, or another generator must see the cases (the provider is public, but `UNI0000`/`UNI9001` suppress the resulting `CA1034`/`CA1515`) |
| D | `Union<T1, T2>` | Exactly two cases, or no generator |

## Checklist

1. A `net11.0` project needs no `LangVersion`; a `net10.0` project sets `LangVersion=latest`. Neither sets
   `EnablePreviewFeatures`.
2. The union is `partial` and is a `struct`, `readonly struct`, `class` or record.
3. Every case type is `public`.
4. A generic union uses surface B or C, never a `typeof` list.
5. The build is clean of `UNI0001`–`UNI0016` and `UNI9000`.

## Where next

- `purview-unions-modelling` — choosing and shaping the case list.
- `purview-unions-diagnostics` — what a `UNI` finding means and how to fix it.
- `purview-unions-interop` — Results, Telemetry, ZodSharp, JSON, trimming and AOT.
