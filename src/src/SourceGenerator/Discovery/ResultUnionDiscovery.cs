using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Purview.Results.SourceGenerator.Diagnostics;
using Purview.Results.SourceGenerator.Helpers;
using Purview.Results.SourceGenerator.Models;

// ITypeSymbol.IsUnion is still marked [Experimental] in Roslyn 5.9, and this discovery deliberately targets
// it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGenerator.Discovery;

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
		var cases = CreateCaseModels(analysis.CaseTypes);
		var includedCases = CreateIncludedCaseModels(analysis.IncludedCases);
		var typeParameters = CreateTypeParameterModels(union);

		return GeneratorResult<ResultUnionModel>.Create(
			new ResultUnionModel(
				Namespace: namespaceName,
				IsGlobalNamespace: namespaceName.Length == 0,
				UnionType: new TypeIdentity(union),
				UnionTypeLiteral: union.ToDisplayString(LiteralFormat),
				IsGeneric: typeParameters.Count > 0,
				TypeParameters: typeParameters,
				UsesMemberProvider: analysis.UsesMemberProvider,
				MemberProviderLiteral: analysis.MemberProvider?.ToDisplayString(LiteralFormat),
				FullyQualifiedName: union.ToDisplayString(ResultUnionDiagnostics.DisplayFormat),
				Accessibility: analysis.UnionIsPublic && analysis.EveryCaseIsPublic
					? TypeDeclarationAccessibility.Public
					: TypeDeclarationAccessibility.Internal,
				ExtensionClassName: extensionClassName,
				HintName: namespaceName.Length == 0
					? extensionClassName + ".g.cs"
					: namespaceName + "." + extensionClassName + ".g.cs",
				Location: ToSourceLocation(union),
				Cases: cases,
				HelperCases: cases,
				IncludedCases: includedCases
			),
			diagnostics
		);
	}

	/// <summary>
	/// The display format used for emitted type names: fully qualified, with C# keywords and nullable
	/// annotations. A type parameter renders as its bare name, so a generic union's own parameters can appear
	/// in a generated top-level helper.
	/// </summary>
	static readonly SymbolDisplayFormat LiteralFormat =
		SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
			SymbolDisplayMiscellaneousOptions.UseSpecialTypes
				| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
				| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
		);

	/// <summary>
	/// Renders a type as a literal, render-only reference.
	/// </summary>
	/// <remarks>
	/// A <c>TypeReference</c> built from a symbol carries a <c>TypeIdentity</c> whose name is empty for a
	/// type parameter, which <c>CodeWriter</c> rejects in a declaration. The generator emits fully qualified
	/// names, so a literal reference over the rendered name is both exact and safe for arrays, nullable
	/// types, constructed generics and type parameters.
	/// </remarks>
	static TypeReference ToLiteral(ITypeSymbol type) =>
		new(new TypeIdentity(type.ToDisplayString(LiteralFormat), null));

	static EquatableArray<ResultUnionTypeParameterModel> CreateTypeParameterModels(INamedTypeSymbol union)
	{
		if (union.TypeParameters.IsDefaultOrEmpty)
			return EquatableArray<ResultUnionTypeParameterModel>.Empty;

		var parameters = ImmutableArray.CreateBuilder<ResultUnionTypeParameterModel>(union.TypeParameters.Length);

		foreach (var parameter in union.TypeParameters)
		{
			var constraints = ImmutableArray.CreateBuilder<string>();

			if (parameter.HasUnmanagedTypeConstraint)
				constraints.Add("unmanaged");
			else if (parameter.HasValueTypeConstraint)
				constraints.Add("struct");
			else if (parameter.HasReferenceTypeConstraint)
				constraints.Add(
					parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
						? "class?"
						: "class"
				);
			else if (parameter.HasNotNullConstraint)
				constraints.Add("notnull");

			foreach (var constraintType in parameter.ConstraintTypes)
				constraints.Add(constraintType.ToDisplayString(LiteralFormat));

			if (parameter.HasConstructorConstraint)
				constraints.Add("new()");

			parameters.Add(new ResultUnionTypeParameterModel(parameter.Name, constraints.ToImmutable()));
		}

		return parameters.ToImmutable();
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

	static EquatableArray<ResultUnionCaseModel> CreateCaseModels(ImmutableArray<ITypeSymbol> caseTypes)
	{
		var cases = ImmutableArray.CreateBuilder<ResultUnionCaseModel>(caseTypes.Length);

		foreach (var caseType in caseTypes)
		{
			cases.Add(
				new ResultUnionCaseModel(
					ToLiteral(caseType),
					caseType.ToDisplayString(ResultUnionDiagnostics.DisplayFormat),
					caseType.IsUnion,
					caseType is ITypeParameterSymbol
				)
			);
		}

		return cases
			.ToImmutable()
			.Sort(static (left, right) => string.CompareOrdinal(left.FullyQualifiedName, right.FullyQualifiedName));
	}

	static EquatableArray<IncludedUnionCaseModel> CreateIncludedCaseModels(
		ImmutableArray<ResultUnionIncludedCase> includedCases
	)
	{
		if (includedCases.IsDefaultOrEmpty)
			return EquatableArray<IncludedUnionCaseModel>.Empty;

		var cases = ImmutableArray.CreateBuilder<IncludedUnionCaseModel>(includedCases.Length);

		foreach (var includedCase in includedCases)
		{
			cases.Add(
				new IncludedUnionCaseModel(
					ToLiteral(includedCase.CaseType),
					includedCase.CaseType.ToDisplayString(ResultUnionDiagnostics.DisplayFormat),
					includedCase
						.UnionPath.Select(static unionType => new IncludedUnionStepModel(
							ToLiteral(unionType),
							ResultUnionDiagnostics.GetMemberProvider(unionType)?.ToDisplayString(LiteralFormat)
						))
						.ToImmutableArray()
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
