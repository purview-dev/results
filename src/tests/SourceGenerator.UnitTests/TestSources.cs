namespace Purview.Results.SourceGenerator;

/// <summary>
/// Source fragments shared by the tests.
/// </summary>
static class TestSources
{
	/// <summary>
	/// Declares the opt-in attribute so analyzer tests can exercise the shared diagnostics library without
	/// running the generator (which is what emits the attribute in a real compilation).
	/// </summary>
	public const string GenerateResultAttributeDeclaration = """
		namespace Purview.Results
		{
			[System.AttributeUsage(
				System.AttributeTargets.Class
					| System.AttributeTargets.Struct
					| System.AttributeTargets.Enum
					| System.AttributeTargets.Interface,
				Inherited = false,
				AllowMultiple = false
			)]
			public sealed class GenerateResultAttribute : System.Attribute;
		}
		""";
}
