using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Purview.Results.SourceGenerator.Models;

/// <summary>
/// A cache-safe description of a source location: the originating file path plus spans.
/// </summary>
/// <remarks>
/// Locations are captured during discovery so that cross-union validation can report diagnostics later.
/// A Roslyn <see cref="Location"/> is deliberately not stored: it would root the syntax tree in the
/// incremental pipeline state.
/// </remarks>
/// <param name="FilePath">The path of the file that contains the location, or an empty string.</param>
/// <param name="TextSpan">The source span of the location.</param>
/// <param name="LinePositionSpan">The line/column span of the location.</param>
readonly record struct ResultSourceLocation(string FilePath, TextSpan TextSpan, LinePositionSpan LinePositionSpan)
{
	/// <summary>
	/// Gets an empty location, used when no source location is available.
	/// </summary>
	public static readonly ResultSourceLocation None;

	/// <summary>
	/// Creates the Roslyn <see cref="Location"/> represented by this value.
	/// </summary>
	/// <returns>
	/// The matching <see cref="Location"/>, or <see cref="Location.None"/> when no location was captured.
	/// </returns>
	/// <remarks>
	/// The file path is empty for path-less syntax trees, which is the case for the in-memory compilations
	/// created by the testing infrastructure. The span is still meaningful there, so a location is created
	/// whenever one was captured.
	/// </remarks>
	public Location ToLocation() =>
		TextSpan == default && LinePositionSpan == default && string.IsNullOrEmpty(FilePath)
			? Location.None
			: Location.Create(FilePath ?? string.Empty, TextSpan, LinePositionSpan);
}

/// <summary>
/// Describes one type parameter of a generic union, including the constraint clauses the generated
/// extension block and helpers must repeat.
/// </summary>
/// <param name="Name">The type parameter name.</param>
/// <param name="Constraints">
/// The constraint expressions written after <c>where {Name} :</c>, in source order (for example
/// <c>class</c>, <c>notnull</c>, a base type, an interface or <c>new()</c>).
/// </param>
readonly record struct ResultUnionTypeParameterModel(string Name, EquatableArray<string> Constraints)
{
	/// <summary>
	/// Gets an empty type-parameter model.
	/// </summary>
	public static readonly ResultUnionTypeParameterModel Empty;
}

/// <summary>
/// Describes one step in an included union's construction path: the union type to construct and whether
/// its creation members live on an <c>IUnionMembers</c> provider.
/// </summary>
/// <param name="UnionType">The literal reference to the union type, including any generic arguments.</param>
/// <param name="MemberProviderLiteral">
/// The fully-qualified literal reference to the union's <c>IUnionMembers</c> provider when its creation
/// members live there; otherwise <see langword="null"/>, meaning a public constructor is used.
/// </param>
readonly record struct IncludedUnionStepModel(TypeReference UnionType, string? MemberProviderLiteral)
{
	/// <summary>
	/// Gets an empty path step.
	/// </summary>
	public static readonly IncludedUnionStepModel Empty;
}

/// <summary>
/// Describes one discovered union case: the type a union value may hold, plus its display name.
/// </summary>
/// <param name="CaseType">
/// The reference to the case type as it is spelled at the generated use site, preserving any nullable
/// annotation and generic construction. A case that names one of the union's own type parameters keeps that
/// parameter in the reference.
/// </param>
/// <param name="FullyQualifiedName">
/// The fully-qualified display name of the case type, used for diagnostics and deterministic ordering.
/// </param>
/// <param name="IsUnionCase">
/// Whether the case type is itself a union. A union-typed case is an <em>included</em> union: its own
/// cases are reachable through it, and sharing it across unions is the intended composition pattern.
/// </param>
/// <param name="IsTypeParameterCase">
/// Whether the case is one of the union's own bare type parameters. Such a case has a union-receiver factory
/// but no per-case <c>AsFailure</c> helper, because an extension method on a bare <c>T</c> would apply to
/// every value.
/// </param>
readonly record struct ResultUnionCaseModel(
	TypeReference CaseType,
	string FullyQualifiedName,
	bool IsUnionCase,
	bool IsTypeParameterCase
)
{
	/// <summary>
	/// Gets an empty case model.
	/// </summary>
	public static readonly ResultUnionCaseModel Empty;
}

/// <summary>
/// Describes one case that a union exposes through an included union rather than declaring directly.
/// </summary>
/// <param name="CaseType">
/// The reference to the transitively reachable case type.
/// </param>
/// <param name="FullyQualifiedName">
/// The fully-qualified display name of the case type, used for deterministic ordering.
/// </param>
/// <param name="UnionPath">
/// The chain of unions that must be constructed to reach the case, innermost first. The generated
/// factory wraps the case in each union in order and finally in the declaring union.
/// </param>
readonly record struct IncludedUnionCaseModel(
	TypeReference CaseType,
	string FullyQualifiedName,
	EquatableArray<IncludedUnionStepModel> UnionPath
)
{
	/// <summary>
	/// Gets an empty included case model.
	/// </summary>
	public static readonly IncludedUnionCaseModel Empty;
}

/// <summary>
/// An immutable, value-equatable description of one opted-in union containing only the information
/// required to generate its result helpers.
/// </summary>
/// <param name="Namespace">The union's containing namespace, or an empty string for the global namespace.</param>
/// <param name="IsGlobalNamespace">Whether the union is declared in the global namespace.</param>
/// <param name="UnionType">The identity of the union type itself, used for equality and ordering.</param>
/// <param name="UnionTypeLiteral">
/// The fully-qualified literal reference to the union, including any generic arguments (for example
/// <c>global::Test.Option&lt;T&gt;</c>). The generated signatures use it verbatim, which is what lets a
/// generic union's own type parameters appear in a top-level extension class.
/// </param>
/// <param name="IsGeneric">Whether the union declares type parameters.</param>
/// <param name="TypeParameters">
/// The union's type parameters and constraints, in declaration order. The generated extension block and
/// per-case helpers repeat them.
/// </param>
/// <param name="UsesMemberProvider">
/// Whether the union's creation members live on a nested <c>IUnionMembers</c> provider instead of public
/// constructors.
/// </param>
/// <param name="MemberProviderLiteral">
/// The fully-qualified literal reference to the <c>IUnionMembers</c> provider when
/// <paramref name="UsesMemberProvider"/> is true; otherwise <see langword="null"/>.
/// </param>
/// <param name="FullyQualifiedName">The union's display name, used for diagnostics.</param>
/// <param name="Accessibility">
/// The accessibility of the generated helper class: never more accessible than the union and every case
/// type it references.
/// </param>
/// <param name="ExtensionClassName">The deterministic name of the generated helper class.</param>
/// <param name="HintName">The deterministic hint name of the generated source file.</param>
/// <param name="Location">The declaration location of the union.</param>
/// <param name="Cases">
/// The union's full set of case types in deterministic (ordinal, fully-qualified name) order. The
/// union-receiver factory is generated for every one of these, including a case type shared with another
/// union.
/// </param>
/// <param name="HelperCases">
/// The subset of <paramref name="Cases"/> whose per-case <c>AsFailure</c> helpers are generated for this
/// union. A leaf case type shared with another union is owned by the union that sorts first, so it is
/// absent here for the others; a union-typed case shared with another union is absent for every union,
/// because its per-case helper would be ambiguous and the union-receiver factory is the safe form. The
/// union-receiver factory still covers every case through <paramref name="Cases"/>.
/// </param>
/// <param name="IncludedCases">
/// The case types reachable through the union's included (union-typed) cases, in deterministic order. The
/// union-receiver factory is generated for each of these as well, so a case of an included union can be
/// converted by naming this union.
/// </param>
readonly record struct ResultUnionModel(
	string Namespace,
	bool IsGlobalNamespace,
	TypeIdentity UnionType,
	string UnionTypeLiteral,
	bool IsGeneric,
	EquatableArray<ResultUnionTypeParameterModel> TypeParameters,
	bool UsesMemberProvider,
	string? MemberProviderLiteral,
	string FullyQualifiedName,
	TypeDeclarationAccessibility Accessibility,
	string ExtensionClassName,
	string HintName,
	ResultSourceLocation Location,
	EquatableArray<ResultUnionCaseModel> Cases,
	EquatableArray<ResultUnionCaseModel> HelperCases,
	EquatableArray<IncludedUnionCaseModel> IncludedCases
)
{
	/// <summary>
	/// Gets an empty union model.
	/// </summary>
	public static readonly ResultUnionModel Empty;
}

/// <summary>
/// The complete set of generation inputs for one compilation: the operational generation context, the
/// validated union models, and every diagnostic to report.
/// </summary>
/// <remarks>
/// The generation context carries the compilation and immutable settings as execution services only; its
/// equality ignores the logger, so logging never invalidates the incremental cache.
/// </remarks>
/// <param name="Context">The operational generation context.</param>
/// <param name="Unions">The unions that should have helpers generated.</param>
/// <param name="Diagnostics">The diagnostics to report for this compilation.</param>
readonly record struct ResultUnionGenerationModel(
	GeneratorContext Context,
	EquatableArray<ResultUnionModel> Unions,
	EquatableArray<ReportableDiagnostic> Diagnostics
);
