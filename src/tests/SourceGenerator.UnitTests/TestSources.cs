namespace Purview.Results.SourceGenerator;

/// <summary>
/// Source fragments shared by the tests.
/// </summary>
static class TestSources
{
	/// <summary>
	/// Declares the <c>Purview.Unions</c> opt-in attribute exactly as its package does, so a test compilation
	/// can exercise the source-visible discovery path without referencing that package (whose analyzer is not
	/// on a CI-safe feed yet). Matching is by metadata identity only, so this declaration is equivalent to the
	/// real attribute for the generator.
	/// </summary>
	public const string GenerateUnionAttributeDeclaration = """
		namespace Purview.Unions
		{
			[System.AttributeUsage(System.AttributeTargets.Struct | System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
			public sealed class GenerateUnionAttribute : System.Attribute
			{
				public GenerateUnionAttribute(params System.Type[] cases) => Cases = cases;

				public System.Collections.Generic.IReadOnlyList<System.Type> Cases { get; }
			}
		}
		""";

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
