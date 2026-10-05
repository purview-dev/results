namespace Purview.Results.SourceGenerator;

/// <summary>
/// Options shared by the results source generator and analyzer tests.
/// </summary>
/// <remarks>
/// The tests compile C# 15 union declarations, so the test compilation uses the preview language version and
/// references the runtime <c>Purview.Results</c> library that the generated helpers target.
/// <para>
/// The options derive from <c>AnalyzerTestOptions</c> so the same type serves both
/// <c>TUnitSourceGeneratorTestBase</c> and <c>TUnitDiagnosticAnalyzerTestBase</c>, which keeps the two hosts
/// under test against identical compilation settings.
/// </para>
/// </remarks>
public record ResultsSourceGeneratorTestOptions : AnalyzerTestOptions
{
	/// <summary>
	/// The MSBuild/analyzer-config property that disables the generator.
	/// </summary>
	public const string DisableGeneratorProperty = "DisableResultsSourceGenerator";

	/// <summary>
	/// The hint names of the sources emitted during post-initialization, which are excluded from the
	/// primary generated source assertions.
	/// </summary>
	public static readonly string[] PostInitializationHintNames =
	[
		"GenerateResultAttribute.g.cs",
		"EmbeddedAttribute.g.cs",
	];

	/// <summary>
	/// Initializes the options used by the generator tests.
	/// </summary>
	public ResultsSourceGeneratorTestOptions()
	{
		// C# 15 union declarations are a preview feature.
		LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview;

		AdditionalAssemblyTypes = AdditionalAssemblyTypes.AddRange(typeof(Result<,>));
		AdditionalNamespaces = AdditionalNamespaces.Add("Purview.Results");

		DisableSourceGeneratorPropertyName = DisableGeneratorProperty;
		ExcludeGeneratedSourceHintNames = [.. PostInitializationHintNames];
	}
}
