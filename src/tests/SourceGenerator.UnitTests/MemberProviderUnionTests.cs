namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated for a custom union whose creation members live on a nested
/// <c>IUnionMembers</c> provider. The generator resolves the case list from the provider's <c>Create</c>
/// methods and constructs values through them.
/// </summary>
public class MemberProviderUnionTests
{
	[Test]
	public async Task UnionFactory_GivenMemberProviderUnion_ProducesValueFailure()
	{
		// Arrange
		ProvidedCase value = new(7);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, ProvidedError> result = ProvidedError.Failure<int>(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is ProvidedCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task AsFailure_GivenMemberProviderUnionCase_ProducesUnitFailure()
	{
		// Arrange
		ProvidedCase value = new(7);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<ProvidedError> result = value.AsFailure();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is ProvidedCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenGenericMemberProviderUnion_ProducesValueFailure()
	{
		// Arrange
		ProvidedSome<int> value = new(7);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<string, ProvidedOption<int>> result = ProvidedOption<int>.Failure<int, string>(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is ProvidedSome<int> { Value: 7 }).IsTrue();
	}
}
