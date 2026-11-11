namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated for the union shapes beyond a plain non-generic struct declaration: a
/// nullable value-type case, a nullable reference-type case, and a class-based union.
/// </summary>
public class UnionShapeTests
{
	[Test]
	public async Task UnionFactory_GivenNullableValueCase_ProducesValueFailure()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, NullableValue> result = NullableValue.Failure<int>(42);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is int { } value && value == 42).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenNullableReferenceCase_ProducesValueFailure()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, NullableValue> result = NullableValue.Failure<int>("present");
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is string { } value && value == "present").IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenClassUnionCase_ProducesValueFailure()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, ClassUnion> result = ClassUnion.Failure<int>(new ClassCase(7));
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is ClassCase { Id: 7 }).IsTrue();
	}

	[Test]
	public async Task UnionFactory_GivenClassUnion_ProducesSuccess()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, ClassUnion> result = ClassUnion.Success(42);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(42);
	}

	[Test]
	public async Task UnionFactory_GivenIncludedGenericUnionCase_ConstructsTheNestedUnion()
	{
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<string, OuterError<int>> result = OuterError<int>.Failure<int, string>(new InnerSome<int>(7));
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert — the outer union holds the inner union, which holds the leaf case.
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is InnerError<int> inner && inner is InnerSome<int> { Value: 7 }).IsTrue();
	}
}
