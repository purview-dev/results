---
name: purview-unions-interop
description: "Use when a Purview.Unions union meets another library — Purview.Results error unions, Purview.Telemetry parameters, ZodSharp, JSON serialisation, trimming and Native AOT — or when deciding which downstream generators can see a generated union's cases."
---

# Purview.Unions interoperability

A union is a plain type carrying `System.Runtime.CompilerServices.UnionAttribute`. Everything that
consumes it does so through ordinary metadata, so interop is mostly about **what is visible in source**.

## The source-visibility rule

Roslyn generators never see each other's output. A downstream generator sees:

- the declaration and the attributes written in the user's source;
- the constructors, `Value`, `HasValue` and `TryGetValue` members **only if the author wrote them**;
- never the members this generator emits.

So a downstream generator can discover a union's cases only when the case list is source-visible. That
is why surfaces B and C exist, and why surface A (`typeof` only) is invisible to them.

## Purview.Results

`[GenerateResult]` recognises a `[GenerateUnion]` target by metadata identity and reads its source-visible
case list instead of generated output. Surface A (`typeof` list), B (partial factory signatures) and C
(`IUnionMembers` provider) all work; no `[Union]` attribute or constructors need to be written by hand. A
`[GenerateUnion]` target with no source-visible case list is `RSG1001`. Surface C remains the portable route
when another generator also needs the case list.

The two-generator integration test is a follow-up in `purview-dev/results` because `Purview.Unions` is not on
a CI-safe package feed yet. Do not work around it by hand-writing the constructors, because that is
`UNI0012`/`UNI0014`.

## Purview.Telemetry

`Purview.Telemetry.SourceGenerator` 6.0.0 recognises a `[GenerateUnion]` target and reads its source-visible
case list (the `typeof` list, partial factories, a nested `IUnionMembers` provider, then source-visible
constructors). It emits the accessor against the generated `HasValue`/`TryGetValue` members, so surface A/B/C
unions are unwrapped to their case value without boxing the union. `just test-telemetry-integration` proves
both generators in one compilation from the packages. A `[GenerateUnion]` target with no source-visible case
list is not treated as a union by Telemetry; the unions generator reports `UNI0003`. A telemetry parameter
typed as `Purview.Unions.Union<T1, T2>` is visible as a union on `net11.0`, where the package assembly carries
`[Union]`.

## ZodSharp

`ZodSharp.Unions.Union<T1, T2>` and `NativeUnion<T1, T2>` predate this package. `Purview.Unions.Union<T1, T2>`
has the same two-case shape and a stable `IUnion` (`Value` + `Tag`). Migrating ZodSharp to it is a
follow-up in `purview-dev/zodsharp`; do not mix the two in one public contract meanwhile.

## JSON, EF Core and ASP.NET Core

Out of scope for v1. A union serialises as the runtime shape of whatever `Value` holds if you serialise
it directly, which is rarely what you want. Map a union to a DTO or a `ProblemDetails` in the host, and
keep the union as the domain contract. EF Core mapping and a `JsonConverter` are follow-ups.

## Trimming and Native AOT

Everything shipped is trim- and AOT-safe:

- the runtime is dependency-free and `IsAotCompatible`;
- `Union<T1, T2>` uses only typed fields, `EqualityComparer<T>.Default` and the static
  `typeof(T).IsValueType`/`Nullable.GetUnderlyingType` metadata checks — no dynamic code, no
  `Type.GetType`, no member reflection;
- generated unions use fields, constructors, a `switch` over a tag and `TryGetValue` overloads.

A trimmed or Native AOT application can use any surface. On .NET 10 the generator emits internal
`[Embedded]` polyfills for `UnionAttribute`/`IUnion`; they do not appear in the public surface.

## .NET 10 versus .NET 11

| | `net10.0` | `net11.0` |
| --- | --- | --- |
| Compiler | C# 15 (`LangVersion=latest`) | C# 15 (the default) |
| `UnionAttribute`/`IUnion` | internal polyfills emitted by the generator | BCL types |
| `Union<T1, T2>` | plain struct with the same API | also `[Union]`/`IUnion`; exhaustive and non-boxing patterns |
| Generated unions | compiler unions via the polyfill | compiler unions via the BCL |

A `net11.0` project needs no `LangVersion`; a `net10.0` project sets `<LangVersion>latest</LangVersion>`
because its default is C# 14. `preview` is not required. Do not set `EnablePreviewFeatures`: it emits
`RequiresPreviewFeatures` assembly-wide and makes `CA2252` fire on every member a consumer touches.

## Checklist

1. If another generator must see the cases, use surface B or C, never surface A.
2. Do not hand-write constructors to satisfy a downstream generator; that is a conflict.
3. Keep unions out of serialisation contracts until a converter exists; map at the boundary.
4. Verify a trimmed/AOT consumer after upgrading the package.
