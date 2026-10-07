using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.Results.SourceGenerator.Diagnostics;

/// <summary>
/// Identifies which host owns a rule and how it affects generation.
/// </summary>
enum DiagnosticScope
{
	/// <summary>
	/// A rule that concerns one opted-in union and can be decided from that union's symbol alone. The
	/// analyzer reports it (IDE feedback and build output); the generator only uses it to decide whether
	/// the union can be processed, so the rule is never reported twice.
	/// </summary>
	Union,

	/// <summary>
	/// A rule that concerns one union case. The analyzer reports it, and generation continues for the
	/// union's remaining cases.
	/// </summary>
	Case,

	/// <summary>
	/// A rule that can only be decided with every opted-in union in the compilation (shared case types and
	/// generated class-name collisions). The generator reports it, because that is where the complete set
	/// of unions is available.
	/// </summary>
	Compilation,
}

/// <summary>
/// The diagnostic catalogue shared by the analyzer (which reports the rules) and the source generator
/// (which uses them to decide whether generation can continue).
/// </summary>
/// <remarks>
/// The blocking policy lives here, next to the descriptors, so both hosts agree on it: <see cref="IsBlocking"/>
/// is the only place that decides whether a finding stops generation, and the analyzer reports exactly the
/// rules the generator does not.
/// </remarks>
static class DiagnosticLibrary
{
	/// <summary>
	/// The diagnostic category used by every rule in this component.
	/// </summary>
	public const string Category = "Purview.Results.Usage";

	/// <summary>
	/// The <c>[GenerateResult]</c> attribute was applied to a type that is not a union.
	/// </summary>
	public static readonly DiagnosticDescriptor TargetIsNotUnion = new(
		id: "RSG1000",
		title: "GenerateResult target is not a union",
		messageFormat: "'{0}' is not a union type; [GenerateResult] can only be applied to a union declaration or a type marked with [Union]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The results source generator only generates helpers for C# 15 union types."
	);

	/// <summary>
	/// The union declares no case types, so there is nothing to generate helpers for.
	/// </summary>
	public static readonly DiagnosticDescriptor UnionHasNoCases = new(
		id: "RSG1001",
		title: "Union has no union cases",
		messageFormat: "'{0}' does not declare any union cases, so no result helpers can be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A union declares its case types through public single-parameter constructors, or through an IUnionMembers member provider."
	);

	/// <summary>
	/// The union or one of its case types is generic, which the first implementation does not support.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedGenericConfiguration = new(
		id: "RSG1002",
		title: "Unsupported generic union configuration",
		messageFormat: "'{0}' is a generic union or has a case type that contains type parameters, which is not supported",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Generic unions cannot be referenced from the generated extension helpers, and neither can a case type that still contains type parameters."
	);

	/// <summary>
	/// A union case type (or the union itself) cannot be referenced from generated code.
	/// </summary>
	public static readonly DiagnosticDescriptor InaccessibleCaseType = new(
		id: "RSG1003",
		title: "Union case type is not accessible",
		messageFormat: "The union case type '{0}' is not accessible from generated code and no result helper can be generated for it",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Generated helpers are emitted as top-level types in the union's namespace, so every type they reference must be public or internal."
	);

	/// <summary>
	/// The same union is configured more than once.
	/// </summary>
	public static readonly DiagnosticDescriptor DuplicateConfiguration = new(
		id: "RSG1004",
		title: "Duplicate GenerateResult configuration",
		messageFormat: "'{0}' is configured more than once with [GenerateResult]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A union must be configured exactly once; duplicate configuration (for example on two partial declarations) would generate the same helpers repeatedly."
	);

	/// <summary>
	/// Two unions map to the same generated helper class name in the same namespace.
	/// </summary>
	public static readonly DiagnosticDescriptor GeneratedClassNameCollision = new(
		id: "RSG1005",
		title: "Generated helper class name collision",
		messageFormat: "The generated helper class '{0}' for '{1}' collides with the helper class generated for '{2}'; helpers are generated for '{2}' only",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Two unions in the same namespace produced the same generated helper class name, so only the first union can be generated."
	);

	/// <summary>
	/// A case type is shared with another union, so the helper would be ambiguous at the call site.
	/// </summary>
	public static readonly DiagnosticDescriptor SharedCaseType = new(
		id: "RSG1006",
		title: "Union case type is shared with another union",
		messageFormat: "The case type '{0}' is also a case of '{1}', so its AsFailure helper is generated for '{1}' only; use the union-receiver factory '{2}.Failure(...)' to convert the case for this union without ambiguity",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A case type may belong to more than one union. Generating the same per-case AsFailure helper for both would make extension method resolution ambiguous, so the helper is generated once. The union-receiver Failure(...) factory is generated for every union, including the shared case, and is the shared-case safe form."
	);

	/// <summary>
	/// The union declares an <c>IUnionMembers</c> member provider, which is not supported.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedMemberProvider = new(
		id: "RSG1007",
		title: "Unsupported union member provider",
		messageFormat: "'{0}' declares an IUnionMembers member provider, which is not supported; declare the union cases as the union's own parameters or constructors",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The first implementation resolves union cases from the union type's own public single-parameter constructors."
	);

	/// <summary>
	/// A case is reachable through more than one of a union's included unions, so no inclusion factory can
	/// be generated for it unambiguously.
	/// </summary>
	public static readonly DiagnosticDescriptor AmbiguousIncludedCase = new(
		id: "RSG1008",
		title: "Union inclusion case is ambiguous",
		messageFormat: "The case type '{0}' is reachable through more than one included union of '{1}', so no inclusion factory is generated for it; convert it through the union that directly declares it instead",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A union may include another union as a case, which exposes the included union's cases through the union-receiver factory. When the same case type is reachable through two included unions, no single factory can name the construction path, so the case is skipped and reported. Only that case's inclusion factory is skipped; the union is still generated."
	);

	/// <summary>
	/// A case type the generated helpers cannot be written against, such as an array type.
	/// </summary>
	/// <remarks>
	/// Accessibility and type-parameter checking both accept an array type — an array of a referenceable
	/// element type is itself referenceable — but the emitted helpers and the union-receiver factory are
	/// declared on a <em>named</em> type. Before this rule existed the generator cast the case to
	/// <c>INamedTypeSymbol</c> regardless and threw <see cref="InvalidCastException"/>, which took the whole
	/// compilation's generated output with it.
	/// </remarks>
	public static readonly DiagnosticDescriptor UnsupportedCaseType = new(
		id: "RSG1009",
		title: "Unsupported union case type",
		messageFormat: "The case type '{0}' is not a named type, so no failure helper or factory can be generated for it; wrap it in a record or struct case instead",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A union case must be a named type for the generated helpers to be declared against it. An array type, for example, is skipped and reported; the union's other cases are still generated."
	);

	/// <summary>
	/// The generator failed unexpectedly while processing a union.
	/// </summary>
	/// <remarks>
	/// Reported instead of letting an exception escape the generator. An escaping exception surfaces as
	/// <c>CS8785</c> and discards <em>all</em> generated output for the compilation — so one malformed union
	/// removes every other union's helpers and produces a cascade of unrelated errors — and raises
	/// <c>AD0001</c> in the IDE, where analysis then stops.
	/// </remarks>
	public static readonly DiagnosticDescriptor UnhandledException = new(
		id: "RSG9000",
		title: "Unhandled exception in the results source generator",
		messageFormat: "The results source generator failed while processing '{0}': {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "An unexpected failure is reported against the union being processed rather than allowed to escape, so the rest of the compilation's generated output survives and the cause is identifiable. Please report it."
	);

	/// <summary>
	/// Gets every rule in the catalogue, which is exactly the analyzer's supported diagnostics.
	/// </summary>
	public static readonly ImmutableArray<DiagnosticDescriptor> AllDescriptors =
	[
		TargetIsNotUnion,
		UnionHasNoCases,
		UnsupportedGenericConfiguration,
		InaccessibleCaseType,
		DuplicateConfiguration,
		GeneratedClassNameCollision,
		SharedCaseType,
		UnsupportedMemberProvider,
		AmbiguousIncludedCase,
		UnsupportedCaseType,
		UnhandledException,
	];

	/// <summary>
	/// Determines whether a finding stops generation, given the scope it was discovered in.
	/// </summary>
	/// <param name="descriptor">The rule that was violated.</param>
	/// <param name="scope">The scope the finding was discovered in.</param>
	/// <returns><see langword="true"/> when generation must stop for the affected target.</returns>
	/// <remarks>
	/// <para>
	/// This is the single blocking policy for both hosts. It is deliberately per-rule rather than
	/// derived from severity: a finding may be an error the consumer must fix while generation is still
	/// able to produce usable output (for example one case type that cannot be referenced, or a duplicate
	/// configuration that is generated once).
	/// </para>
	/// <para>
	/// Blocking decisions:
	/// <list type="bullet">
	/// <item><description><c>RSG1000</c> (not a union): nothing can be generated.</description></item>
	/// <item><description><c>RSG1001</c> (no cases): nothing can be generated.</description></item>
	/// <item><description><c>RSG1002</c>: blocking for a generic union, non-blocking for a single case type that still contains type parameters — the remaining cases are generated.</description></item>
	/// <item><description><c>RSG1003</c>: blocking when the union itself cannot be referenced, non-blocking for a single inaccessible case type.</description></item>
	/// <item><description><c>RSG1004</c> (duplicate configuration): non-blocking; the union is valid and its helpers are generated once.</description></item>
	/// <item><description><c>RSG1005</c> (generated class-name collision): blocking for the colliding union, which is skipped.</description></item>
	/// <item><description><c>RSG1006</c> (shared case type): non-blocking; only the shared case's helper is skipped.</description></item>
	/// <item><description><c>RSG1007</c> (member provider): nothing can be generated.</description></item>
	/// <item><description><c>RSG1008</c> (ambiguous included case): non-blocking; only the ambiguous case's inclusion factory is skipped.</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0072:Add missing cases",
		Justification = "A new rule must add an explicit blocking decision; the fallback keeps unknown rules severity-based."
	)]
	public static bool IsBlocking(DiagnosticDescriptor descriptor, DiagnosticScope scope) =>
		descriptor.Id switch
		{
			"RSG1000" => true,
			"RSG1001" => true,
			"RSG1002" => scope == DiagnosticScope.Union,
			"RSG1003" => scope == DiagnosticScope.Union,
			"RSG1004" => false,
			"RSG1005" => true,
			"RSG1006" => false,
			"RSG1007" => true,
			"RSG1008" => false,
			_ => descriptor.DefaultSeverity == DiagnosticSeverity.Error,
		};
}
