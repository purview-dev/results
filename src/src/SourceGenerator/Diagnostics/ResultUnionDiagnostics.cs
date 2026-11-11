using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.Results.SourceGenerator.Helpers;

// ITypeSymbol.IsUnion is still marked [Experimental] in Roslyn 5.9, and this analysis deliberately targets
// it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGenerator.Diagnostics;

/// <summary>
/// Describes one case a union exposes through an included (union-typed) case rather than declaring it
/// directly, together with the chain of unions that must be constructed to reach it.
/// </summary>
/// <param name="CaseType">The transitively reachable case type.</param>
/// <param name="UnionPath">
/// The chain of unions that must be constructed to reach the case, innermost first. The declaring union is
/// not part of the path; the generated factory wraps the case in each path union and finally in the
/// declaring union.
/// </param>
readonly record struct ResultUnionIncludedCase(INamedTypeSymbol CaseType, ImmutableArray<INamedTypeSymbol> UnionPath);

/// <summary>
/// The outcome of analyzing one opted-in target, shared by the analyzer (which reports
/// <see cref="Diagnostics"/>) and the source generator (which uses them to decide whether the target can be
/// generated and which case types it should reference).
/// </summary>
/// <param name="HasGenerateResultAttribute">Whether the target opts in with the generated attribute.</param>
/// <param name="IsUnion">
/// Whether the target is a union this generator recognises: a language union or a source-visible
/// <c>Purview.Unions.GenerateUnionAttribute</c> target.
/// </param>
/// <param name="UnionIsReferenceable">Whether the union itself can be referenced from generated code.</param>
/// <param name="UnionIsPublic">Whether the union is public.</param>
/// <param name="EveryCaseIsPublic">Whether every usable case type is public.</param>
/// <param name="CaseTypes">The usable case types, in declaration order.</param>
/// <param name="IncludedCases">
/// The case types reachable through the union's included (union-typed) cases, in discovery order.
/// </param>
/// <param name="UsesMemberProvider">
/// Whether the union's creation members live on a nested <c>IUnionMembers</c> provider.
/// </param>
/// <param name="MemberProvider">The union's nested <c>IUnionMembers</c> provider, when it has one.</param>
/// <param name="Diagnostics">Every finding, in discovery order.</param>
readonly record struct ResultUnionTargetAnalysis(
	bool HasGenerateResultAttribute,
	bool IsUnion,
	bool UnionIsReferenceable,
	bool UnionIsPublic,
	bool EveryCaseIsPublic,
	ImmutableArray<ITypeSymbol> CaseTypes,
	ImmutableArray<ResultUnionIncludedCase> IncludedCases,
	bool UsesMemberProvider,
	INamedTypeSymbol? MemberProvider,
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
/// <para>
/// A target is a union when the language says so (<see cref="ITypeSymbol.IsUnion"/>) or when it opts into
/// another generator with <c>Purview.Unions.GenerateUnionAttribute</c>. The latter is matched by metadata
/// identity only, because this generator never sees another generator's output: a <c>[GenerateUnion]</c>
/// type has no visible <c>[Union]</c> attribute and no visible constructors in this compilation. Never
/// infer union membership from type names, source-text parsing, reflection or the union's runtime value.
/// </para>
/// <para>
/// A language union resolves its cases from the language's own rule — a nested <c>IUnionMembers</c>
/// provider when one exists, otherwise each public constructor with exactly one by-value (or <c>in</c>)
/// parameter. A <c>[GenerateUnion]</c> target resolves the first non-empty <em>source-visible</em> list
/// instead: the attribute's <c>typeof</c> list, its partial static factory signatures, a nested
/// <c>IUnionMembers</c> provider, then any source-visible public single-parameter constructors.
/// </para>
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
	/// Determines whether the target opts into union generation with
	/// <c>Purview.Unions.GenerateUnionAttribute</c>.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <returns><see langword="true"/> when the attribute is applied.</returns>
	/// <remarks>
	/// The attribute is matched by metadata identity only. Its application is source-visible, so a target
	/// is recognisable as a union even though the <c>[Union]</c> attribute and the constructors the union
	/// generator emits are not visible to this generator.
	/// </remarks>
	public static bool HasGenerateUnionAttribute(INamedTypeSymbol symbol) =>
		HasAttribute(symbol, ResultsTypeLibrary.GenerateUnionAttribute);

	/// <summary>
	/// Determines whether the type is a union as far as this generator is concerned: a language union
	/// (a <c>union</c> declaration or a source-visible <c>[Union]</c> type) or a type opted into union
	/// generation with <c>Purview.Unions.GenerateUnionAttribute</c>.
	/// </summary>
	/// <param name="type">The type to test.</param>
	/// <returns><see langword="true"/> when the type is a union this generator can read cases from.</returns>
	public static bool IsUnionType(INamedTypeSymbol type) =>
		type.IsUnion || HasAttribute(type, ResultsTypeLibrary.UnionAttribute) || HasGenerateUnionAttribute(type);

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
				IncludedCases: [],
				UsesMemberProvider: false,
				MemberProvider: null,
				Diagnostics: []
			);
		}

		var displayName = symbol.ToDisplayString(DisplayFormat);
		var declarationLocation = symbol.Locations.IsDefaultOrEmpty ? Location.None : symbol.Locations[0];
		var attributeLocation = GetAttributeLocation(symbol, cancellationToken) ?? declarationLocation;

		if (!IsUnionType(symbol))
		{
			return new ResultUnionTargetAnalysis(
				HasGenerateResultAttribute: true,
				IsUnion: false,
				UnionIsReferenceable: false,
				UnionIsPublic: false,
				EveryCaseIsPublic: false,
				CaseTypes: [],
				IncludedCases: [],
				UsesMemberProvider: false,
				MemberProvider: null,
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

		var memberProvider = GetMemberProvider(symbol);
		if (memberProvider is not null && GetProviderCaseTypes(memberProvider).IsDefaultOrEmpty)
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

		if (HasGenericContainingType(symbol))
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

		var typeParameters = symbol.TypeParameters;
		if (diagnostics.Any(static diagnostic => diagnostic.IsBlocking))
		{
			return new ResultUnionTargetAnalysis(
				HasGenerateResultAttribute: true,
				IsUnion: true,
				UnionIsReferenceable: unionIsReferenceable,
				UnionIsPublic: unionIsPublic,
				EveryCaseIsPublic: false,
				CaseTypes: [],
				IncludedCases: [],
				UsesMemberProvider: memberProvider is not null,
				MemberProvider: memberProvider,
				Diagnostics: diagnostics.ToImmutable()
			);
		}

		var everyCaseIsPublic = true;
		var caseTypes = AnalyzeCases(
			symbol,
			memberProvider,
			attributeLocation,
			typeParameters,
			diagnostics,
			ref everyCaseIsPublic,
			cancellationToken
		);
		var includedCases = AnalyzeIncludedCases(
			symbol,
			caseTypes,
			attributeLocation,
			diagnostics,
			ref everyCaseIsPublic,
			cancellationToken
		);

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
			IncludedCases: includedCases,
			UsesMemberProvider: memberProvider is not null,
			MemberProvider: memberProvider,
			Diagnostics: diagnostics.ToImmutable()
		);
	}

	/// <summary>
	/// Resolves the union's usable case types from its source-visible case list, and reports case findings
	/// (which never block the union) for the cases that cannot be referenced from generated code.
	/// </summary>
	/// <remarks>
	/// A language union keeps the language's rule: a nested <c>IUnionMembers</c> provider's <c>Create</c>
	/// methods when one exists, otherwise the union's public single-parameter constructors. A
	/// <c>[GenerateUnion]</c> target reads the first non-empty source-visible list the union generator
	/// supports — the attribute's <c>typeof</c> list, its partial static factory signatures, a nested
	/// <c>IUnionMembers</c> provider, or any source-visible public single-parameter constructors — because
	/// the union's own constructors live in another generator's output.
	/// </remarks>
	static ImmutableArray<ITypeSymbol> AnalyzeCases(
		INamedTypeSymbol union,
		INamedTypeSymbol? memberProvider,
		Location fallbackLocation,
		ImmutableArray<ITypeParameterSymbol> typeParameters,
		ImmutableArray<ResultDiagnostic>.Builder diagnostics,
		ref bool everyCaseIsPublic,
		CancellationToken cancellationToken
	)
	{
		var caseTypes = ImmutableArray.CreateBuilder<ITypeSymbol>();
		var candidates = GetCaseCandidates(union, memberProvider, fallbackLocation, cancellationToken);

		foreach (var (candidateType, caseLocation) in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var caseType = NormalizeCaseType(candidateType);
			var displayName = caseType.ToDisplayString(DisplayFormat);

			// A bare type parameter is a valid union case only when it is one of the union's own type
			// parameters. It gets a union-receiver factory but no per-case AsFailure helper.
			if (caseType is ITypeParameterSymbol parameter)
			{
				if (!typeParameters.Contains(parameter, SymbolEqualityComparer.Default))
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

				caseTypes.Add(parameter);
				continue;
			}

			// A case may mention the union's own type parameters, but a type parameter supplied by an
			// enclosing type cannot be declared by the generated top-level helper.
			if (ContainsExternalTypeParameters(caseType, typeParameters))
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

			// The accessibility and type-parameter checks above both accept an array type, so the case can
			// reach here without being a named type. The generated helpers and the union-receiver factory
			// are declared against a named type, so report and skip rather than casting — the cast threw
			// InvalidCastException out of the generator, which discarded every other union's output too.
			if (caseType is not INamedTypeSymbol namedCaseType)
			{
				diagnostics.Add(
					ResultDiagnostic.Create(
						DiagnosticLibrary.UnsupportedCaseType,
						DiagnosticScope.Case,
						caseLocation,
						displayName
					)
				);
				continue;
			}

			everyCaseIsPublic &= caseIsPublic;
			caseTypes.Add(namedCaseType);
		}

		return caseTypes.ToImmutable();
	}

	/// <summary>
	/// Expands the union's included (union-typed) cases transitively, so the union-receiver factory can
	/// convert a case of an included union by naming this union.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A union-typed case is an <em>inclusion</em>: the value is wrapped in the included union, so the
	/// runtime value is nested (<c>Outer(Inner(leaf))</c>). The recorded path is that nesting, innermost
	/// first, and the generated factory wraps the case in each union in order.
	/// </para>
	/// <para>
	/// A case reachable through two different included unions cannot be constructed unambiguously; it is
	/// reported as <c>RSG1008</c> and its inclusion factory is skipped. A case whose construction path
	/// passes through an ambiguous union is skipped for the same reason.
	/// </para>
	/// </remarks>
	static ImmutableArray<ResultUnionIncludedCase> AnalyzeIncludedCases(
		INamedTypeSymbol union,
		ImmutableArray<ITypeSymbol> caseTypes,
		Location reportLocation,
		ImmutableArray<ResultDiagnostic>.Builder diagnostics,
		ref bool everyCaseIsPublic,
		CancellationToken cancellationToken
	)
	{
		HashSet<ITypeSymbol> directCases = [with(SymbolEqualityComparer.Default), .. caseTypes];

		Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> paths = [with(SymbolEqualityComparer.Default)];
		HashSet<INamedTypeSymbol> ambiguous = [with(SymbolEqualityComparer.Default)];
		HashSet<INamedTypeSymbol> stack = [with(SymbolEqualityComparer.Default)];
		var typeParameters = union.TypeParameters;

		foreach (var caseType in caseTypes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (caseType is INamedTypeSymbol unionCase && IsUnionType(unionCase))
				Walk(unionCase, []);
		}

		if (paths.Count == 0)
			return [];

		var results = ImmutableArray.CreateBuilder<ResultUnionIncludedCase>(paths.Count);

		foreach (
			var pair in paths.OrderBy(static pair => pair.Key.ToDisplayString(DisplayFormat), StringComparer.Ordinal)
		)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var caseType = pair.Key;
			var path = pair.Value;

			// A case is ambiguous when it is reachable through two included unions, or when any union in its
			// construction path is itself ambiguous: the second path would construct a different value.
			if (ambiguous.Contains(caseType) || path.Any(ambiguous.Contains))
				continue;

			// Every type the generated factory references must be referenceable from generated code; a
			// non-public one narrows the generated class rather than dropping the case.
			var referenceable = TryGetReferenceableAccessibility(caseType, out var caseIsPublic);
			foreach (var pathUnion in path)
			{
				if (!TryGetReferenceableAccessibility(pathUnion, out var pathIsPublic))
				{
					referenceable = false;
					break;
				}

				caseIsPublic &= pathIsPublic;
			}

			if (!referenceable)
				continue;

			everyCaseIsPublic &= caseIsPublic;
			results.Add(new ResultUnionIncludedCase(caseType, path));
		}

		foreach (var caseType in ambiguous)
		{
			diagnostics.Add(
				ResultDiagnostic.Create(
					DiagnosticLibrary.AmbiguousIncludedCase,
					DiagnosticScope.Union,
					reportLocation,
					caseType.ToDisplayString(DisplayFormat),
					union.ToDisplayString(DisplayFormat)
				)
			);
		}

		return results.ToImmutable();

		void Walk(INamedTypeSymbol currentUnion, ImmutableArray<INamedTypeSymbol> parentPath)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// The declaring union is never its own inclusion, and a union already on the current path is a
			// cycle: neither can be constructed.
			if (SymbolEqualityComparer.Default.Equals(currentUnion, union))
				return;

			if (!stack.Add(currentUnion))
				return;

			foreach (var caseType in GetUsableCaseTypes(currentUnion, typeParameters, cancellationToken))
			{
				if (
					SymbolEqualityComparer.Default.Equals(caseType, union)
					|| directCases.Contains(caseType)
					|| stack.Contains(caseType)
				)
					continue;

				var path = ImmutableArray.Create(currentUnion).AddRange(parentPath);

				if (paths.TryGetValue(caseType, out var existing))
				{
					if (!PathsEqual(existing, path))
						ambiguous.Add(caseType);

					continue;
				}

				paths.Add(caseType, path);

				if (IsUnionType(caseType))
					Walk(caseType, path);
			}

			stack.Remove(currentUnion);
		}
	}

	/// <summary>
	/// Resolves an included union's usable case types without reporting findings, which are reported when
	/// that union is itself analyzed as an opted-in target.
	/// </summary>
	static ImmutableArray<INamedTypeSymbol> GetUsableCaseTypes(
		INamedTypeSymbol union,
		ImmutableArray<ITypeParameterSymbol> allowedTypeParameters,
		CancellationToken cancellationToken
	)
	{
		var caseTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

		foreach (var candidateType in GetCaseTypeCandidates(union, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var caseType = NormalizeCaseType(candidateType);

			if (caseType is ITypeParameterSymbol || ContainsExternalTypeParameters(caseType, allowedTypeParameters))
				continue;

			if (!TryGetReferenceableAccessibility(caseType, out _))
				continue;

			// An array type passes both checks above but is not a named type. This path walks included
			// unions and reports nothing of its own, so the case is simply skipped; the declaring union's
			// own analysis reports it as RSG1009.
			if (caseType is not INamedTypeSymbol namedCaseType)
				continue;

			caseTypes.Add(namedCaseType);
		}

		return caseTypes.ToImmutable();
	}

	/// <summary>
	/// Gets the union's case type candidates from the first non-empty source-visible case list.
	/// </summary>
	/// <remarks>
	/// A <c>[GenerateUnion]</c> target mirrors the union generator's order: the attribute's <c>typeof</c>
	/// list, its partial static factory signatures, a nested <c>IUnionMembers</c> provider, then any
	/// source-visible public single-parameter constructors. A language union keeps the language's rule: the
	/// provider when one exists, otherwise its constructors.
	/// </remarks>
	static List<(ITypeSymbol CaseType, Location Location)> GetCaseCandidates(
		INamedTypeSymbol union,
		INamedTypeSymbol? memberProvider,
		Location fallbackLocation,
		CancellationToken cancellationToken
	)
	{
		if (HasGenerateUnionAttribute(union))
		{
			var attributeCases = GetAttributeCaseTypes(union);
			if (!attributeCases.IsDefaultOrEmpty)
				return [.. attributeCases.Select(caseType => (caseType, fallbackLocation))];

			var factoryCases = GetFactoryCaseCandidates(union, cancellationToken);
			if (factoryCases.Count > 0)
				return factoryCases;
		}

		if (memberProvider is not null)
		{
			List<(ITypeSymbol CaseType, Location Location)> providerCases = [];

			foreach (var method in GetProviderCreateMethods(memberProvider))
			{
				cancellationToken.ThrowIfCancellationRequested();
				providerCases.Add((method.Parameters[0].Type, GetParameterLocation(method.Parameters[0])));
			}

			if (providerCases.Count > 0)
				return providerCases;
		}

		List<(ITypeSymbol CaseType, Location Location)> constructorCases = [];

		foreach (var constructor in union.InstanceConstructors)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (IsCaseConstructor(constructor))
				constructorCases.Add((constructor.Parameters[0].Type, GetParameterLocation(constructor.Parameters[0])));
		}

		return constructorCases;
	}

	/// <summary>
	/// Gets an included union's case type candidates without reporting findings.
	/// </summary>
	static IEnumerable<ITypeSymbol> GetCaseTypeCandidates(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		foreach (var candidate in GetCaseCandidates(union, GetMemberProvider(union), Location.None, cancellationToken))
			yield return candidate.CaseType;
	}

	/// <summary>
	/// Reads the <c>[GenerateUnion]</c> <c>typeof</c> case list, in declaration order.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetAttributeCaseTypes(INamedTypeSymbol union)
	{
		foreach (var attribute in union.GetAttributes())
		{
			if (!ResultsTypeLibrary.GenerateUnionAttribute.Matches(attribute.AttributeClass))
				continue;

			if (attribute.ConstructorArguments.Length != 1)
				return [];

			var argument = attribute.ConstructorArguments[0];
			if (argument.Kind != TypedConstantKind.Array || argument.IsNull)
				return [];

			var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(argument.Values.Length);

			foreach (var value in argument.Values)
			{
				if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
					builder.Add(type);
			}

			return builder.ToImmutable();
		}

		return [];
	}

	/// <summary>
	/// Reads the case types from the union's own partial static factory signatures.
	/// </summary>
	static List<(ITypeSymbol CaseType, Location Location)> GetFactoryCaseCandidates(
		INamedTypeSymbol union,
		CancellationToken cancellationToken
	)
	{
		List<(ITypeSymbol CaseType, Location Location)> candidates = [];

		foreach (var method in OrderedFactoryMethods(union, cancellationToken))
			candidates.Add((method.Parameters[0].Type, GetParameterLocation(method.Parameters[0])));

		return candidates;
	}

	/// <summary>
	/// Gets the partial static factory signatures to read cases from, in source order.
	/// </summary>
	/// <remarks>
	/// A factory is a static method with exactly one unrestricted parameter whose return type is the union,
	/// declared as a <c>partial</c> signature without a body. This mirrors the union generator, so a
	/// generated union's source-visible factories are the same list that generator implements.
	/// </remarks>
	static IEnumerable<IMethodSymbol> OrderedFactoryMethods(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		foreach (var member in OrderedMembers(union))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method)
				continue;

			if (method.Parameters[0].RefKind is not (RefKind.None or RefKind.In))
				continue;

			if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, union))
				continue;

			if (!IsPartialMethodDefinition(method, cancellationToken))
				continue;

			yield return method;
		}
	}

	static IEnumerable<ISymbol> OrderedMembers(INamedTypeSymbol type) =>
		type.GetMembers()
			.OrderBy(static member =>
				member.Locations.IsDefaultOrEmpty ? int.MaxValue : member.Locations[0].SourceSpan.Start
			);

	/// <summary>
	/// Determines whether a method is a <c>partial</c> signature without a body, which is what makes a
	/// factory declaration a case list rather than an implementation.
	/// </summary>
	static bool IsPartialMethodDefinition(IMethodSymbol method, CancellationToken cancellationToken)
	{
		foreach (var reference in method.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax(cancellationToken) is MethodDeclarationSyntax syntax
				&& syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
				&& syntax.Body is null
				&& syntax.ExpressionBody is null
			)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Normalises a case type exactly as the union generator exposes it: a nullable value type becomes its
	/// underlying type, a nullable reference annotation is stripped, and constructed generic types and
	/// arrays are preserved as they are.
	/// </summary>
	static ITypeSymbol NormalizeCaseType(ITypeSymbol caseType) =>
		caseType switch
		{
			INamedTypeSymbol named
				when named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
					&& named.TypeArguments.Length == 1 => NormalizeCaseType(named.TypeArguments[0]),
			_ when caseType.NullableAnnotation == NullableAnnotation.Annotated => caseType.WithNullableAnnotation(
				NullableAnnotation.NotAnnotated
			),
			_ => caseType,
		};

	static bool HasAttribute(INamedTypeSymbol type, TypeIdentity identity)
	{
		foreach (var attribute in type.GetAttributes())
		{
			if (identity.Matches(attribute.AttributeClass))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Gets the union's nested <c>IUnionMembers</c> provider, when it declares one.
	/// </summary>
	/// <param name="union">The union to inspect.</param>
	/// <returns>The provider interface, or <see langword="null"/>.</returns>
	/// <remarks>
	/// The language finds a union's members only on a public nested interface named <c>IUnionMembers</c>.
	/// A type that declares one is a member-provider union even when it also has constructors.
	/// </remarks>
	public static INamedTypeSymbol? GetMemberProvider(INamedTypeSymbol union)
	{
		foreach (var member in union.GetTypeMembers("IUnionMembers"))
		{
			if (!member.IsImplicitlyDeclared && member.TypeKind == TypeKind.Interface)
				return member;
		}

		return null;
	}

	/// <summary>
	/// Determines whether the union is nested inside a generic type. The generated helper is a top-level type,
	/// so it cannot name the containing type's type parameters; such a union is reported rather than emitted
	/// with an unbound type argument.
	/// </summary>
	static bool HasGenericContainingType(INamedTypeSymbol union)
	{
		for (var current = union.ContainingType; current is not null; current = current.ContainingType)
		{
			if (current.Arity > 0)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Gets every static single-parameter <c>Create</c> method declared by a provider or an interface it
	/// inherits. The parameter type of each is a case type.
	/// </summary>
	static IEnumerable<IMethodSymbol> GetProviderCreateMethods(INamedTypeSymbol provider)
	{
		foreach (var current in provider.AllInterfaces.Prepend(provider))
		{
			foreach (var member in current.GetMembers("Create"))
			{
				if (member is IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method)
					yield return method;
			}
		}
	}

	/// <summary>
	/// Gets a provider's case types, used to decide whether the provider declares the required creation
	/// members.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetProviderCaseTypes(INamedTypeSymbol provider) =>
		[.. GetProviderCreateMethods(provider).Select(static method => method.Parameters[0].Type)];

	static Location GetParameterLocation(IParameterSymbol parameter) =>
		parameter.Locations.IsDefaultOrEmpty ? Location.None : parameter.Locations[0];

	static bool PathsEqual(ImmutableArray<INamedTypeSymbol> left, ImmutableArray<INamedTypeSymbol> right)
	{
		if (left.Length != right.Length)
			return false;

		for (var index = 0; index < left.Length; index++)
		{
			if (!SymbolEqualityComparer.Default.Equals(left[index], right[index]))
				return false;
		}

		return true;
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
	/// Determines whether a type contains a type parameter that the generated helper cannot declare: one
	/// that is not supplied by the union itself (directly, through an array element, or through a generic
	/// type argument).
	/// </summary>
	static bool ContainsExternalTypeParameters(
		ITypeSymbol type,
		ImmutableArray<ITypeParameterSymbol> allowedTypeParameters
	)
	{
		switch (type)
		{
			case ITypeParameterSymbol parameter:
				return !allowedTypeParameters.Contains(parameter, SymbolEqualityComparer.Default);

			case IArrayTypeSymbol array:
				return ContainsExternalTypeParameters(array.ElementType, allowedTypeParameters);

			case INamedTypeSymbol named:
			{
				if (named.IsUnboundGenericType)
					return true;

				foreach (var argument in named.TypeArguments)
				{
					if (ContainsExternalTypeParameters(argument, allowedTypeParameters))
						return true;
				}

				return named.ContainingType is not null
					&& ContainsExternalTypeParameters(named.ContainingType, allowedTypeParameters);
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

			// The caller has already rejected a type parameter the generated helper cannot declare; a type
			// parameter that reaches here is the union's own, and is referenceable by name.
			case ITypeParameterSymbol:
				return true;

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
