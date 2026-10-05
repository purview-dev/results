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
/// Describes one discovered union case: the type a union value may hold, plus its display name.
/// </summary>
/// <param name="CaseType">
/// The reference to the case type as it is spelled at the generated use site, preserving any nullable
/// annotation and generic construction.
/// </param>
/// <param name="FullyQualifiedName">
/// The fully-qualified display name of the case type, used for diagnostics and deterministic ordering.
/// </param>
readonly record struct ResultUnionCaseModel(TypeReference CaseType, string FullyQualifiedName)
{
	/// <summary>
	/// Gets an empty case model.
	/// </summary>
	public static readonly ResultUnionCaseModel Empty;
}

/// <summary>
/// An immutable, value-equatable description of one opted-in union containing only the information
/// required to generate its result helpers.
/// </summary>
/// <param name="Namespace">The union's containing namespace, or an empty string for the global namespace.</param>
/// <param name="IsGlobalNamespace">Whether the union is declared in the global namespace.</param>
/// <param name="UnionType">The identity of the union type itself.</param>
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
/// union. A case type shared with another union is owned by the union that sorts first, so it is absent here
/// for the others; the union-receiver factory still covers it through <paramref name="Cases"/>.
/// </param>
readonly record struct ResultUnionModel(
	string Namespace,
	bool IsGlobalNamespace,
	TypeIdentity UnionType,
	string FullyQualifiedName,
	TypeDeclarationAccessibility Accessibility,
	string ExtensionClassName,
	string HintName,
	ResultSourceLocation Location,
	EquatableArray<ResultUnionCaseModel> Cases,
	EquatableArray<ResultUnionCaseModel> HelperCases
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
