# Diagnostics

The union rules live in one shared library (`Diagnostics/DiagnosticLibrary.cs`,
`Diagnostics/ResultUnionDiagnostics.cs`, `Diagnostics/ResultDiagnostic.cs`) that both hosts consume:

- **`ResultsDiagnosticAnalyzer` reports the per-target rules** (`RSG1000`–`RSG1004`, `RSG1007`) in the IDE and in
  build output.
- **The generator reports the compilation-wide rules** (`RSG1005`, `RSG1006`) that need every opted-in union in
  the compilation.
- The generator never reports a rule the analyzer reports. It still runs the same shared analysis, so
  `DiagnosticLibrary.IsBlocking` is the single blocking policy, but a finding is never surfaced twice.

## Rules

| ID | Severity | Reported by | Blocking | Description |
| --- | --- | --- | --- | --- |
| `RSG1000` | Error | Analyzer | Yes | `[GenerateResult]` was applied to a type that is not a union. |
| `RSG1001` | Error | Analyzer | Yes | The union declares no union cases. |
| `RSG1002` | Error | Analyzer | Union: yes / case: no | A generic union, or a case type that contains type parameters. |
| `RSG1003` | Error | Analyzer | Union: yes / case: no | A type that generated code must reference is not accessible (for example a `file` type). |
| `RSG1004` | Error | Analyzer | No | The same union is configured more than once (for example on two partial declarations); the helpers are generated once. |
| `RSG1005` | Error | Generator | Yes (the colliding union is skipped) | Two unions produce the same generated class name in one namespace. |
| `RSG1006` | Warning | Generator | No (only the shared case's helper is skipped) | A case type is shared with another union, so its helper is generated once to keep call sites unambiguous. |
| `RSG1007` | Error | Analyzer | Yes | The union uses an `IUnionMembers` member provider, which the first implementation does not support. |

Case-level findings never block the union: the remaining cases are still generated and the skipped case is
reported. Blocking is decided per rule in `DiagnosticLibrary.IsBlocking`, not derived from severity, because a
finding may be an error the consumer must fix while usable output can still be produced.

Every rule is categorized as `Purview.Results.Usage` and tracked in
`src/src/SourceGenerator/AnalyzerReleases.Unshipped.md`, which the compiler's RS2008 rule catalogue validates.

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
  suppression id rather than a rule, so `RSG1000`–`RSG1007` remain the only diagnostics the package reports.

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
