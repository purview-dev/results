# Diagnostics

The union rules live in one shared library (`Diagnostics/DiagnosticLibrary.cs`,
`Diagnostics/ResultUnionDiagnostics.cs`, `Diagnostics/ResultDiagnostic.cs`) that both hosts consume:

- **`ResultsDiagnosticAnalyzer` reports the per-target rules** (`RSG1000`–`RSG1004`, `RSG1007`–`RSG1009`) in the
  IDE and in build output.
- **The generator reports the compilation-wide rules** (`RSG1005`, `RSG1006`) that need every opted-in union in
  the compilation, and `RSG9000` when the emitter fails unexpectedly.
- The generator never reports a rule the analyzer reports. It still runs the same shared analysis, so
  `DiagnosticLibrary.IsBlocking` is the single blocking policy, but a finding is never surfaced twice.

## Rules

| ID | Severity | Reported by | Blocking | Description |
| --- | --- | --- | --- | --- |
| `RSG1000` | Error | Analyzer | Yes | `[GenerateResult]` was applied to a type that is not a union. A union is a C# 15 `union` declaration, a `[Union]` type, or a source-visible `Purview.Unions` `[GenerateUnion]` type. |
| `RSG1001` | Error | Analyzer | Yes | The union declares no source-visible cases: no `[GenerateUnion]` `typeof` list, partial static factory signatures, `IUnionMembers` provider or public single-parameter constructors. |
| `RSG1002` | Error | Analyzer | No (only the offending case is skipped) | A case type names a type parameter the union does not declare, or the union is nested in a generic type. A generic union itself is supported. |
| `RSG1003` | Error | Analyzer | Union: yes / case: no | A type that generated code must reference is not accessible (for example a `file` type). |
| `RSG1004` | Error | Analyzer | No | The same union is configured more than once (for example on two partial declarations); the helpers are generated once. |
| `RSG1005` | Error | Generator | Yes (the colliding union is skipped) | Two unions produce the same generated class name in one namespace. |
| `RSG1006` | Warning | Generator | No (only the shared leaf case's `AsFailure` helper is skipped) | A **leaf** case type is shared with another union, so its per-case helper is generated once; the union-receiver `Failure(...)` factory is generated for every union and is the shared-case safe form. A union-typed case shared across unions is composition, not a shared leaf, and is not reported. |
| `RSG1007` | Error | Analyzer | Yes | The union's `IUnionMembers` provider declares no public static single-parameter `Create` method. |
| `RSG1008` | Warning | Analyzer | No (only the ambiguous case's inclusion factory is skipped) | A case is reachable through two different included unions, so no inclusion factory can name its construction path. List the case through the union that directly declares it instead. |
| `RSG1009` | Warning | Analyzer | No (only the unsupported case is skipped) | A case type is not a named type (for example an array), so no helper or factory can be declared against it. Wrap it in a record or struct case instead. |
| `RSG9000` | Error | Generator | — | The generator failed unexpectedly. The failure is reported instead of escaping, because an escaping exception surfaces as `CS8785` and discards *all* generated output for the compilation. Please report it. |

Case-level findings never block the union: the remaining cases are still generated and the skipped case is
reported. Blocking is decided per rule in `DiagnosticLibrary.IsBlocking`, not derived from severity, because a
finding may be an error the consumer must fix while usable output can still be produced.

Every rule is categorized as `Purview.Results.Usage` and release-tracked, which the compiler's RS2008 rule
catalogue validates: the shipped rules are recorded in `src/src/SourceGenerator/AnalyzerReleases.Shipped.md` under
Release 1.0.0, and a new or changed rule goes into `AnalyzerReleases.Unshipped.md` until the next release.

## Diagnostic suppressions

A union declaration gets no value equality from the compiler, so every union raises `CA1815` ("Override equals
and operator equals on value types") unless it is silenced by hand with a `#pragma warning disable CA1815` or a
`[SuppressMessage]`. A `[GenerateResult]` union is the error type of a result and is read by matching its case
type, not by comparing two unions by value, so the package answers that warning:

| Suppression ID | Suppressed rule | Applies to |
| --- | --- | --- |
| `RSG2000` | `CA1815` | A declaration that is a union **and** is opted in with `[GenerateResult]` |

The suppression is narrow on purpose:

- A union without `[GenerateResult]` keeps the warning — the package only speaks for the unions it generates for.
- The union's **case types** keep the warning, as does every other value type. A case declared as a plain `struct`
  still gets `CA1815`; a `record struct` case never gets it, because records synthesise equality.
- Nothing is hidden: a suppressor can only suppress non-error, configurable diagnostics, and `RSG2000` is a
  suppression id rather than a rule, so `RSG1000`–`RSG1009` and `RSG9000` remain the only diagnostics the
  package reports.

Every suppression is logged as an `Info` diagnostic against `RSG2000`, in the verbose build log and in an MSBuild
binlog (and as a suppressed diagnostic in an `/errorlog` SARIF file), so a build can always be audited for what it
suppressed.

### Keeping `CA1815`

Add `RSG2000` to the compiler's warning suppressions, and the warning returns for every union declaration,
including the opted-in ones:

```xml
<PropertyGroup>
	<NoWarn>$(NoWarn);RSG2000</NoWarn>
</PropertyGroup>
```

Declaring equality on the union is the other way to satisfy `CA1815` — it is the code the warning asks for, and a
union that has it never raises the warning, so the suppressor has nothing to do.

## Related

- [Union Errors](Union-Errors.md) — the union shapes the rules describe.
- [Source Generator](Source-Generator.md) — where each host runs and how output is produced.
- [Testing](Testing.md) — how the analyzer, suppressor and code fix are verified.
