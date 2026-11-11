namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated for union-shaped types whose cases come from a source-visible
/// <c>[GenerateUnion]</c> list rather than source constructors.
/// </summary>
public class GenerateUnionDiscoveryRuntimeTests
{
	[Test]
	public async Task AsFailure_GivenGenerateUnionTypeofListCase_ProducesValueFailure()
	{
		// Arrange
		DiscoveredSourceCase value = new(7);

		// Act
		// The explicit type proves the helper produces the result for the discovered union.
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, DiscoveredError> result = value.AsFailure<int>();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		// The test stand-in has no [Union] attribute (the real generator would emit one), so the active case
		// is read through its Value property rather than by union pattern matching.
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.Value is DiscoveredSourceCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenGenerateUnionTypeofListCase_ProducesValueFailure()
	{
		// Arrange
		DiscoveredSourceCase value = new(7);

		// Act
		// The union-receiver factory names the union discovered from the typeof list.
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, DiscoveredError> result = DiscoveredError.Failure<int>(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.Value is DiscoveredSourceCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task AsFailure_GivenGenerateUnionPartialFactoryCase_ProducesValueFailure()
	{
		// Arrange
		FactoryDiscoveredCase value = new(7);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, FactoryDiscoveredError> result = value.AsFailure<int>();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.Value is FactoryDiscoveredCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenGenerateUnionPartialFactoryCase_ProducesUnitFailure()
	{
		// Arrange
		FactoryDiscoveredOtherCase value = new("rejected");

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<FactoryDiscoveredError> result = FactoryDiscoveredError.Failure(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.Value is FactoryDiscoveredOtherCase { Reason: "rejected" }).IsTrue();
	}
}
