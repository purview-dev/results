namespace Purview.Results.SourceGenerator.Helpers;

/// <summary>
/// Provides the <c>TypeIdentity</c> values and namespaces the results source generator resolves
/// against a compilation.
/// </summary>
/// <remarks>
/// Only reusable framework identities live in the Source Generator Framework's own catalogue; the
/// Library-specific identities below stay in this component so the framework remains generic.
/// </remarks>
static class ResultsTypeLibrary
{
	/// <summary>
	/// The namespace that contains the union contracts the language uses to describe unions.
	/// </summary>
	public const string RuntimeCompilerServicesNamespace = "System.Runtime.CompilerServices";

	/// <summary>
	/// The generated <c>GenerateResultAttribute</c> opt-in attribute.
	/// </summary>
	public static readonly TypeIdentity GenerateResultAttribute = new(
		PropertyLibrary.GenerateResultAttributeName,
		PropertyLibrary.ResultsNamespace
	);

	/// <summary>
	/// The runtime <c>Result&lt;TValue, TError&gt;</c> result type. Callers apply the generic arguments
	/// for a specific union.
	/// </summary>
	public static readonly TypeIdentity Result = new(
		typeName: "Result",
		@namespace: PropertyLibrary.ResultsNamespace,
		arity: 2
	);

	/// <summary>
	/// The language-specific <c>System.Runtime.CompilerServices.IUnion</c> interface implemented by
	/// declared unions.
	/// </summary>
	public static readonly TypeIdentity Union = new(typeName: "IUnion", @namespace: RuntimeCompilerServicesNamespace);

	/// <summary>
	/// The language-specific <c>System.Runtime.CompilerServices.UnionAttribute</c> attribute applied to
	/// union types.
	/// </summary>
	public static readonly TypeIdentity UnionAttribute = new(
		typeName: "UnionAttribute",
		@namespace: RuntimeCompilerServicesNamespace
	);
}
