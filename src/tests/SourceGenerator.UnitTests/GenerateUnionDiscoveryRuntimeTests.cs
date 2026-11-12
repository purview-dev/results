namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated for unions produced by the real <c>Purview.Unions</c> generator, whose cases
/// the results generator discovered from the source-visible list rather than from generated constructors.
/// </summary>
public class GenerateUnionDiscoveryRuntimeTests
{
	[Test]
	public async Task AsFailure_GivenGenerateUnionTypeofListCase_ProducesValueFailure()
	{
		// Arrange
		DiscoveredSourceCase value = new(7);

		// Act
		var result = value.AsFailure<int>();

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out DiscoveredSourceCase activeCase)).IsTrue();
		await Assert.That(activeCase.Id).IsEqualTo(7);
	}

	[Test]
	public async Task UnionFactory_GivenGenerateUnionTypeofListCase_ProducesValueFailure()
	{
		// Arrange
		DiscoveredSourceCase value = new(7);

		// Act
		// The union-receiver factory names the union discovered from the typeof list.
		var result = DiscoveredError.Failure<int>(value);

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out DiscoveredSourceCase activeCase)).IsTrue();
		await Assert.That(activeCase.Id).IsEqualTo(7);
	}

	[Test]
	public async Task AsFailure_GivenGenerateUnionPartialFactoryCase_ProducesValueFailure()
	{
		// Arrange
		FactoryDiscoveredCase value = new(7);

		// Act
		var result = value.AsFailure<int>();

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out FactoryDiscoveredCase activeCase)).IsTrue();
		await Assert.That(activeCase.Id).IsEqualTo(7);
	}

	[Test]
	public async Task UnionFactory_GivenGenerateUnionPartialFactoryCase_ProducesUnitFailure()
	{
		// Arrange
		FactoryDiscoveredOtherCase value = new("rejected");

		// Act
		var result = FactoryDiscoveredError.Failure(value);

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out FactoryDiscoveredOtherCase activeCase)).IsTrue();
		await Assert.That(activeCase.Reason).IsEqualTo("rejected");
	}

	[Test]
	public async Task AsFailure_GivenGenerateUnionProviderCase_ProducesValueFailure()
	{
		// Arrange
		ProviderDiscoveredCase value = new(7);

		// Act
		// The provider surface is the portable route when another generator must also see the case list.
		var result = value.AsFailure<int>();

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out ProviderDiscoveredCase activeCase)).IsTrue();
		await Assert.That(activeCase.Id).IsEqualTo(7);
	}

	[Test]
	public async Task UnionFactory_GivenGenerateUnionProviderCase_ProducesUnitFailure()
	{
		// Arrange
		ProviderDiscoveredOtherCase value = new("rejected");

		// Act
		var result = ProviderDiscoveredError.Failure(value);

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out ProviderDiscoveredOtherCase activeCase)).IsTrue();
		await Assert.That(activeCase.Reason).IsEqualTo("rejected");
	}
}
