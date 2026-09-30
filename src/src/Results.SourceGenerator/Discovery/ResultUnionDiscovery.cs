using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Purview.Results.SourceGeneration.Diagnostics;
using Purview.Results.SourceGeneration.Helpers;
using Purview.Results.SourceGeneration.Models;

namespace Purview.Results.SourceGeneration.Discovery;

/// <summary>
/// Converts an opted-in attribute target into the immutable <see cref="ResultUnionModel"/> the pipeline
/// caches.
/// </summary>
/// <remarks>
/// Every semantic decision — union membership, the case set, accessibility, and the findings that decide
/// whether generation can continue — is made by <see cref="ResultUnionDiagnostics"/>, which the analyzer
/// uses as well. This type only projects that analysis into a value model plus the deterministic generated
/// names, so there is no second implementation of the union rules.
/// </remarks>
static class ResultUnionDiscovery
{
	/// <summary>
	/// Creates the union model for a single attribute target, carrying the findings that decide whether the
	/// target can be processed.
	/// </summary>
	/// <param name="context">The attribute syntax context supplied by the incremental pipeline.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A result carrying the model, or a result that only carries blocking findings.</returns>
	public static GeneratorResult<ResultUnionModel> CreateTarget(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (context.TargetSymbol is not INamedTypeSymbol union)
			return GeneratorResult<ResultUnionModel>.Empty;

		var analysis = ResultUnionDiagnostics.Analyze(union, cancellationToken);

		// The findings travel with the result so GeneratorResult.ShouldProcess applies the shared blocking
		// policy. They are not reported from here: the analyzer reports the per-target rules and the
		// validator reports the compilation-wide ones, so no rule is reported twice.
		var diagnostics = ToReportableDiagnostics(analysis.Diagnostics);

		if (!analysis.CanGenerate)
			return GeneratorResult<ResultUnionModel>.Create(ResultUnionModel.Empty, diagnostics);

		var namespaceName = union.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: union.ContainingNamespace.ToDisplayString();

		var extensionClassName = CreateExtensionClassName(union);

		return GeneratorResult<ResultUnionModel>.Create(
			new ResultUnionModel(
				Namespace: namespaceName,
				IsGlobalNamespace: namespaceName.Length == 0,
				UnionType: new TypeIdentity(union),
				FullyQualifiedName: union.ToDisplayString(ResultUnionDiagnostics.DisplayFormat),
				Accessibility: analysis.UnionIsPublic && analysis.EveryCaseIsPublic
					? TypeDeclarationAccessibility.Public
					: TypeDeclarationAccessibility.Internal,
				ExtensionClassName: extensionClassName,
				HintName: namespaceName.Length == 0
					? extensionClassName + ".g.cs"
					: namespaceName + "." + extensionClassName + ".g.cs",
				Location: ToSourceLocation(union),
				Cases: CreateCaseModels(analysis.CaseTypes)
			),
			diagnostics
		);
	}

	static EquatableArray<ReportableDiagnostic> ToReportableDiagnostics(ImmutableArray<ResultDiagnostic> findings)
	{
		if (findings.IsDefaultOrEmpty)
			return EquatableArray<ReportableDiagnostic>.Empty;

		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>(findings.Length);
		foreach (var finding in findings)
			diagnostics.Add(finding.ToReportableDiagnostic());

		return diagnostics.ToImmutable();
	}

	static EquatableArray<ResultUnionCaseModel> CreateCaseModels(ImmutableArray<INamedTypeSymbol> caseTypes)
	{
		var cases = ImmutableArray.CreateBuilder<ResultUnionCaseModel>(caseTypes.Length);

		foreach (var caseType in caseTypes)
		{
			cases.Add(
				new ResultUnionCaseModel(
					TypeReference.Create(caseType),
					caseType.ToDisplayString(ResultUnionDiagnostics.DisplayFormat)
				)
			);
		}

		return cases
			.ToImmutable()
			.Sort(static (left, right) => string.CompareOrdinal(left.FullyQualifiedName, right.FullyQualifiedName));
	}

	/// <summary>
	/// Creates the deterministic extension class name for a union, including its containing type chain so
	/// that nested unions cannot collide with a top-level union of the same name.
	/// </summary>
	static string CreateExtensionClassName(INamedTypeSymbol union)
	{
		StringBuilder builder = new();

		var containingTypes = ContainingTypes(union);
		for (var index = containingTypes.Length - 1; index >= 0; index--)
			builder.Append(containingTypes[index].Name).Append('_');

		builder.Append(union.Name).Append(PropertyLibrary.ExtensionClassSuffix);

		return builder.ToString();
	}

	static ImmutableArray<INamedTypeSymbol> ContainingTypes(INamedTypeSymbol union)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

		for (var current = union.ContainingType; current is not null; current = current.ContainingType)
			builder.Add(current);

		return builder.ToImmutable();
	}

	/// <summary>
	/// Captures a symbol's declaration location as a cache-safe value: model state only ever holds the file
	/// path and spans, never a Roslyn <see cref="Location"/>.
	/// </summary>
	static ResultSourceLocation ToSourceLocation(ISymbol symbol) =>
		ToSourceLocation(symbol.Locations.IsDefaultOrEmpty ? null : symbol.Locations[0]);

	static ResultSourceLocation ToSourceLocation(Location? location) =>
		location?.SourceTree is null
			? ResultSourceLocation.None
			: new ResultSourceLocation(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
}
