using Microsoft.CodeAnalysis;
using Purview.Results.SourceGeneration.Helpers;
using System.Collections.Immutable;

// The language's union support is a preview feature of C# 15; ITypeSymbol.IsUnion is marked
// [Experimental] until the feature ships, and this analysis deliberately targets it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGeneration.Diagnostics;

/// <summary>
/// The outcome of analyzing one opted-in target, shared by the analyzer (which reports
/// <see cref="Diagnostics"/>) and the source generator (which uses them to decide whether the target can be
/// generated and which case types it should reference).
/// </summary>
/// <param name="HasGenerateResultAttribute">Whether the target opts in with the generated attribute.</param>
/// <param name="IsUnion">Whether the target is a C# union.</param>
/// <param name="UnionIsReferenceable">Whether the union itself can be referenced from generated code.</param>
/// <param name="UnionIsPublic">Whether the union is public.</param>
/// <param name="EveryCaseIsPublic">Whether every usable case type is public.</param>
/// <param name="CaseTypes">The usable case types, in constructor order.</param>
/// <param name="Diagnostics">Every finding, in discovery order.</param>
readonly record struct ResultUnionTargetAnalysis(
	bool HasGenerateResultAttribute,
	bool IsUnion,
	bool UnionIsReferenceable,
	bool UnionIsPublic,
	bool EveryCaseIsPublic,
	ImmutableArray<INamedTypeSymbol> CaseTypes,
	ImmutableArray<ResultDiagnostic> Diagnostics
)
{
	/// <summary>
	/// Gets whether any finding stops generation for the target.
	/// </summary>
	public bool HasBlockingDiagnostics =>
		!Diagnostics.IsDefaultOrEmpty && Diagnostics.Any(static diagnostic => diagnostic.IsBlocking);

	/// <summary>
	/// Gets whether helpers can be generated for the target: it is a referenceable union with at least one
	/// usable case type and no blocking finding.
	/// </summary>
	public bool CanGenerate =>
		IsUnion && UnionIsReferenceable && !CaseTypes.IsDefaultOrEmpty && !HasBlockingDiagnostics;
}

/// <summary>
/// Analyzes a single opted-in target. This is the one implementation of the per-target rules, used by both
/// the analyzer and the source generator so the two hosts cannot drift apart.
/// </summary>
/// <remarks>
/// Union membership is decided by the language through <see cref="ITypeSymbol.IsUnion"/> — never by type
/// name conventions, source-text parsing, reflection or runtime inspection of the union's value. Case types
/// are resolved using the language's own rule: for each public constructor with exactly one by-value (or
/// <c>in</c>) parameter, the type of that parameter is a case type of the union.
/// </remarks>
static class ResultUnionDiagnostics
{
	/// <summary>
	/// The display format used for diagnostics (and for the models' display names), which includes generic
	/// arguments, language keywords and escaped identifiers.
	/// </summary>
	public static readonly SymbolDisplayFormat DisplayFormat = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
			| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
	);

	/// <summary>
	/// Determines whether the target opts into generation with the generated attribute.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <returns><see langword="true"/> when the opt-in attribute is applied.</returns>
	public static bool HasGenerateResultAttribute(INamedTypeSymbol symbol) => CountGenerateResultAttributes(symbol) > 0;

	/// <summary>
	/// Counts the opt-in attribute applications on the target. Attribute applications on separate partial
	/// declarations are counted separately, which is what makes duplicate configuration detectable.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <returns>The number of applications.</returns>
	public static int CountGenerateResultAttributes(INamedTypeSymbol symbol)
	{
		var count = 0;

		foreach (var attribute in symbol.GetAttributes())
		{
			if (ResultsTypeLibrary.GenerateResultAttribute.Matches(attribute.AttributeClass))
				count++;
		}

		return count;
	}

	/// <summary>
	/// Gets the location of the first opt-in attribute application, falling back to the declaration so a
	/// finding always points at the offending code where possible.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>The location, or <see langword="null"/> when none is available.</returns>
	public static Location? GetAttributeLocation(INamedTypeSymbol symbol, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		foreach (var attribute in symbol.GetAttributes())
		{
			var syntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken);
			if (syntax is not null)
				return syntax.GetLocation();
		}

		return symbol.Locations.IsDefaultOrEmpty ? null : symbol.Locations[0];
	}

	/// <summary>
	/// Analyzes one target.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>The analysis, including every finding and the usable case types.</returns>
	public static ResultUnionTargetAnalysis Analyze(INamedTypeSymbol symbol, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var attributeCount = CountGenerateResultAttributes(symbol);
		if (attributeCount == 0)
		{
			return new ResultUnionTargetAnalysis(
				HasGenerateResultAttribute: false,
				IsUnion: false,
				UnionIsReferenceable: false,
				UnionIsPublic: false,
				EveryCaseIsPublic: false,
				CaseTypes: [],
				Diagnostics: []
			);
		}

		var displayName = symbol.ToDisplayString(DisplayFormat);
		var declarationLocation = symbol.Locations.IsDefaultOrEmpty ? Location.None : symbol.Locations[0];
		var attributeLocation = GetAttributeLocation(symbol, cancellationToken) ?? declarationLocation;

		if (!symbol.IsUnion)
		{
			return new ResultUnionTargetAnalysis(
				HasGenerateResultAttribute: true,
				IsUnion: false,
				UnionIsReferenceable: false,
				UnionIsPublic: false,
				EveryCaseIsPublic: false,
				CaseTypes: [],
				Diagnostics:
				[
					ResultDiagnostic.Create(
						DiagnosticLibrary.TargetIsNotUnion,
						DiagnosticScope.Union,
						attributeLocation,
						displayName
					),
				]
			);
		}

		var diagnostics = ImmutableArray.CreateBuilder<ResultDiagnostic>();

		if (attributeCount > 1)
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.DuplicateConfiguration,
					DiagnosticScope.Union,
					attributeLocation,
					displayName
				)
			);
		}

		if (HasNonImplicitMemberProvider(symbol))
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.UnsupportedMemberProvider,
					DiagnosticScope.Union,
					declarationLocation,
					displayName
				)
			);
		}

		if (symbol.IsGenericType || symbol.Arity > 0)
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.UnsupportedGenericConfiguration,
					DiagnosticScope.Union,
					declarationLocation,
					displayName
				)
			);
		}

		var unionIsReferenceable = TryGetReferenceableAccessibility(symbol, out var unionIsPublic);
		if (!unionIsReferenceable)
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.InaccessibleCaseType,
					DiagnosticScope.Union,
					declarationLocation,
					displayName
				)
			);
		}

		if (diagnostics.Any(static diagnostic => diagnostic.IsBlocking))
		{
			return new ResultUnionTargetAnalysis(
				HasGenerateResultAttribute: true,
				IsUnion: true,
				UnionIsReferenceable: unionIsReferenceable,
				UnionIsPublic: unionIsPublic,
				EveryCaseIsPublic: false,
				CaseTypes: [],
				Diagnostics: diagnostics.ToImmutable()
			);
		}

		var everyCaseIsPublic = true;
		var caseTypes = AnalyzeCases(symbol, diagnostics, ref everyCaseIsPublic, cancellationToken);

		if (caseTypes.Length == 0)
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.UnionHasNoCases,
					DiagnosticScope.Union,
					declarationLocation,
					displayName
				)
			);
		}

		return new ResultUnionTargetAnalysis(
			HasGenerateResultAttribute: true,
			IsUnion: true,
			UnionIsReferenceable: unionIsReferenceable,
			UnionIsPublic: unionIsPublic,
			EveryCaseIsPublic: everyCaseIsPublic,
			CaseTypes: caseTypes,
			Diagnostics: diagnostics.ToImmutable()
		);
	}

	/// <summary>
	/// Resolves the union's usable case types from the language's case-type rule, reporting case findings
	/// (which never block the union) for the cases that cannot be referenced from generated code.
	/// </summary>
	static ImmutableArray<INamedTypeSymbol> AnalyzeCases(
		INamedTypeSymbol union,
		ImmutableArray<ResultDiagnostic>.Builder diagnostics,
		ref bool everyCaseIsPublic,
		CancellationToken cancellationToken
	)
	{
		var caseTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

		foreach (var constructor in union.InstanceConstructors)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!IsCaseConstructor(constructor))
				continue;

			var caseType = constructor.Parameters[0].Type;
			var caseLocation = constructor.Parameters[0].Locations.IsDefaultOrEmpty
				? Location.None
				: constructor.Parameters[0].Locations[0];
			var displayName = caseType.ToDisplayString(DisplayFormat);

			if (ContainsTypeParameters(caseType))
			{
				diagnostics.Add(
					ResultDiagnostic.Create(
						DiagnosticLibrary.UnsupportedGenericConfiguration,
						DiagnosticScope.Case,
						caseLocation,
						displayName
					)
				);
				continue;
			}

			if (!TryGetReferenceableAccessibility(caseType, out var caseIsPublic))
			{
				diagnostics.Add(
					ResultDiagnostic.Create(
						DiagnosticLibrary.InaccessibleCaseType,
						DiagnosticScope.Case,
						caseLocation,
						displayName
					)
				);
				continue;
			}

			everyCaseIsPublic &= caseIsPublic;
			caseTypes.Add((INamedTypeSymbol)caseType);
		}

		return caseTypes.ToImmutable();
	}

	/// <summary>
	/// Determines whether a constructor follows the language's union case pattern: a public, single
	/// by-value (or <c>in</c>) parameter constructor whose parameter type is therefore a case type.
	/// </summary>
	static bool IsCaseConstructor(IMethodSymbol constructor) =>
		constructor.DeclaredAccessibility == Accessibility.Public
		&& constructor.Parameters.Length == 1
		&& constructor.Parameters[0].RefKind is RefKind.None or RefKind.In;

	/// <summary>
	/// Determines whether the union directly declares a user-authored <c>IUnionMembers</c> member provider
	/// interface, which the first implementation does not support.
	/// </summary>
	static bool HasNonImplicitMemberProvider(INamedTypeSymbol union)
	{
		foreach (var member in union.GetTypeMembers("IUnionMembers"))
		{
			if (!member.IsImplicitlyDeclared)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Determines whether a type contains a type parameter (directly, through an array element, or through
	/// a generic type argument), which cannot be referenced from a generated top-level extension method.
	/// </summary>
	static bool ContainsTypeParameters(ITypeSymbol type)
	{
		switch (type)
		{
			case ITypeParameterSymbol:
				return true;

			case IArrayTypeSymbol array:
				return ContainsTypeParameters(array.ElementType);

			case INamedTypeSymbol named:
				{
					if (named.IsUnboundGenericType)
						return true;

					foreach (var argument in named.TypeArguments)
					{
						if (ContainsTypeParameters(argument))
							return true;
					}

					return named.ContainingType is not null && ContainsTypeParameters(named.ContainingType);
				}

			default:
				return false;
		}
	}

	/// <summary>
	/// Determines whether a type can be referenced from generated code that is emitted as a top-level type
	/// in the union's namespace, and whether every type it references is public.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <param name="isPublic">Whether the type and everything it references is public.</param>
	static bool TryGetReferenceableAccessibility(ITypeSymbol type, out bool isPublic)
	{
		isPublic = true;

		switch (type)
		{
			case IArrayTypeSymbol array:
				return TryGetReferenceableAccessibility(array.ElementType, out isPublic);

			case INamedTypeSymbol named:
				{
					if (named.IsFileLocal || named.IsUnboundGenericType)
						return false;

					foreach (var argument in named.TypeArguments)
					{
						if (!TryGetReferenceableAccessibility(argument, out var argumentIsPublic))
							return false;

						isPublic &= argumentIsPublic;
					}

					for (var current = named; current is not null; current = current.ContainingType)
					{
						var accessibility = current.DeclaredAccessibility;
						if (accessibility == Accessibility.Public)
							continue;

						// protected internal is accessible from anywhere in this assembly, which is where
						// generated code lives; every other non-public accessibility is not referenceable
						// from a generated top-level type.
						if (accessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal)
						{
							isPublic = false;
							continue;
						}

						return false;
					}

					return true;
				}

			default:
				// Type parameters, pointers, function pointers, dynamic and error types cannot be
				// referenced as a generated extension method receiver.
				return false;
		}
	}
}
