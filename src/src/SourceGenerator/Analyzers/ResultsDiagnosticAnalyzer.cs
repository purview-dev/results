using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Results.SourceGeneration.Diagnostics;

namespace Purview.Results.SourceGeneration.Analyzers;

/// <summary>
/// Raises the per-target result diagnostics so consumers see them in the IDE and in build output.
/// </summary>
/// <remarks>
/// <para>
/// The analyzer and the source generator share one diagnostics library
/// (<see cref="DiagnosticLibrary"/> plus <see cref="ResultUnionDiagnostics"/>). The analyzer is the single
/// reporting source for the per-target rules (<c>RSG1000</c>–<c>RSG1004</c> and <c>RSG1007</c>), while the
/// generator uses exactly the same findings to decide whether generation can continue and reports only the
/// compilation-wide rules (<c>RSG1005</c>, <c>RSG1006</c>) that need every opted-in union. That split keeps
/// blocking decisions consistent between the two hosts and never reports a rule twice.
/// </para>
/// <para>
/// The analyzer does not compute case types, names or generated source; it only asks the shared library for
/// findings, so there is no second implementation of the union rules to drift.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultsDiagnosticAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticLibrary.AllDescriptors;

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1062:Validate arguments of public methods",
		Justification = "The Roslyn contract states that AnalysisContext is never null."
	)]
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		if (context.Symbol is not INamedTypeSymbol symbol)
			return;

		// Cheap gate before the shared analysis: most types in a compilation do not opt in.
		if (!ResultUnionDiagnostics.HasGenerateResultAttribute(symbol))
			return;

		var analysis = ResultUnionDiagnostics.Analyze(symbol, context.CancellationToken);
		if (analysis.Diagnostics.IsDefaultOrEmpty)
			return;

		foreach (var finding in analysis.Diagnostics)
			context.ReportDiagnostic(finding.ToDiagnostic());
	}
}
