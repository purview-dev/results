; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md under Release 1.0.0. Add a new or changed
; diagnostic here, and move it across when the next version ships.
;
; RSG1000, RSG1001, RSG1002 and RSG1007 keep the same category, severity and enabled-by-default state; only
; their message and meaning changed (generic unions, IUnionMembers providers and source-visible
; Purview.Unions [GenerateUnion] targets are now supported), so no release-tracking entry is required.
;
; Note RSG2000 is the suppression id a consumer adds to NoWarn to turn the CA1815 suppression off.
; Suppression descriptors are not release-tracked, so it must not be listed in either file.
