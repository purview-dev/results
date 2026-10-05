namespace Purview.Results.SourceGenerator.Helpers;

/// <summary>
/// Contains the names, hint names and build properties used by the results source generator.
/// </summary>
static class PropertyLibrary
{
	/// <summary>
	/// The namespace of the runtime <c>Purview.Results</c> library that defines
	/// <c>Result&lt;TValue, TError&gt;</c>.
	/// </summary>
	public const string ResultsNamespace = "Purview.Results";

	/// <summary>
	/// The metadata name of the generated opt-in attribute.
	/// </summary>
	public const string GenerateResultAttributeName = "GenerateResultAttribute";

	/// <summary>
	/// The MSBuild property that disables the generator when set to <see langword="true"/>.
	/// </summary>
	public const string DisableGenerator = "DisableResultsSourceGenerator";

	/// <summary>
	/// The hint name of the generated opt-in attribute source.
	/// </summary>
	public const string AttributeHintName = GenerateResultAttributeName + ".g.cs";

	/// <summary>
	/// The suffix applied to the name of each generated extension class.
	/// </summary>
	public const string ExtensionClassSuffix = "ResultExtensions";

	/// <summary>
	/// The name of the generated union-case to failure helper method.
	/// </summary>
	public const string FailureHelperName = "AsFailure";

	/// <summary>
	/// The name of the generic value type parameter used by the generated helpers.
	/// </summary>
	public const string ValueTypeParameterName = "TValue";

	/// <summary>
	/// The name of the generated helper's union-case parameter.
	/// </summary>
	public const string ErrorParameterName = "error";

	/// <summary>
	/// The fully-qualified name of the runtime <c>Result&lt;TValue, TError&gt;</c> type, used when the
	/// generated source is described in XML documentation. The value is raw, so callers escape it with
	/// <see cref="DocText.Escape"/> before it is written into documentation text.
	/// </summary>
	public const string ResultTypeName = ResultsNamespace + ".Result<TValue, TError>";
}
