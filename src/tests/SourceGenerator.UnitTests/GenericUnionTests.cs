namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated for a generic C# 15 union. The union's own type parameter is declared by
/// the generated extension block and by every per-case helper, so a call supplies the union arguments first
/// and the successful value type last.
/// </summary>
public class GenericUnionTests
{
	[Test]
	public async Task UnionFactory_GivenGenericUnionCase_ProducesValueFailure()
	{
		// Arrange
		Some<string> value = new("present");

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, Maybe<string>> result = Maybe<string>.Failure<string, int>(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is Some<string> { Value: "present" }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenGenericUnionUnitCase_ProducesUnitFailure()
	{
		// Arrange
		None value = default;

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Maybe<string>> result = Maybe<string>.Failure(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is None).IsTrue();
	}

	[Test]
	public async Task AsFailure_GivenGenericUnionCase_DeclaresTheUnionParameterAndValueType()
	{
		// Arrange
		Some<string> value = new("present");

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, Maybe<string>> result = value.AsFailure<string, int>();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is Some<string> { Value: "present" }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenGenericUnion_ProducesSuccess()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Maybe<string>> unit = Maybe<string>.Success();
		Result<int, Maybe<string>> value = Maybe<string>.Success(42);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(unit.IsSuccess).IsTrue();
		await Assert.That(value.IsSuccess).IsTrue();
		await Assert.That(value.Value).IsEqualTo(42);
	}

	[Test]
	public async Task UnionFactory_GivenBareTypeParameterCase_ProducesValueFailure()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, TypedOrText<int>> result = TypedOrText<int>.Failure<int, int>(42);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is int { } value && value == 42).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenConstrainedGenericUnion_RepeatsTheConstraint()
	{
		// Arrange
		ConstrainedSome<string> value = new("present");

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, ConstrainedMaybe<string>> result = ConstrainedMaybe<string>.Failure<string, int>(value);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is ConstrainedSome<string> { Value: "present" }).IsTrue();
	}
}
