namespace Purview.Unions;

/// <summary>
/// A test stand-in for the <c>Purview.Unions</c> opt-in attribute.
/// </summary>
/// <remarks>
/// The real package's generator is not on a CI-safe feed yet, so the test project declares the attribute with
/// the same metadata identity. The results generator matches it by metadata name only, exactly as it would
/// match the packaged attribute.
/// </remarks>
/// <param name="cases">The case types of the generated union.</param>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
// Deliberately internal, which is the default for a top-level type: the generator test compilations declare
// their own public stand-in from a source string, and an imported public copy would conflict (CS0436) with it.
sealed class GenerateUnionAttribute(params Type[] cases) : Attribute
{
	/// <summary>
	/// Gets the case types of the generated union, in declaration order.
	/// </summary>
	public IReadOnlyList<Type> Cases { get; } = cases;
}
