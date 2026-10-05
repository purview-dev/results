; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
RSG1000 | Purview.Results.Usage | Error | GenerateResult target is not a union
RSG1001 | Purview.Results.Usage | Error | Union has no union cases
RSG1002 | Purview.Results.Usage | Error | Unsupported generic union configuration
RSG1003 | Purview.Results.Usage | Error | Union case type is not accessible
RSG1004 | Purview.Results.Usage | Error | Duplicate GenerateResult configuration
RSG1005 | Purview.Results.Usage | Error | Generated helper class name collision
RSG1006 | Purview.Results.Usage | Warning | Union case type is shared with another union
RSG1007 | Purview.Results.Usage | Error | Unsupported union member provider
RSG1008 | Purview.Results.Usage | Warning | Union inclusion case is ambiguous
