using ZodSharp.Core;

namespace Purview.Results.ZodSharp;

/// <summary>
/// Tests for <see cref="ZodValidationResultExtensions"/>: a validation outcome becomes a result state, and no
/// error is created when validation succeeds.
/// </summary>
public class ZodValidationResultExtensionsTests
{
	[Test]
	public async Task ToResult_GivenSuccessfulValidation_ReturnsTheValidatedValueWithoutInvokingTheFactory()
	{
		// Arrange
		var validation = ValidationResult<int>.Success(42);
		var factoryInvocations = 0;

		// Act
		var result = validation.ToResult(errors =>
		{
			factoryInvocations++;
			return errors.Length;
		});

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(42);
		await Assert.That(factoryInvocations).IsEqualTo(0);
	}

	[Test]
	public async Task ToResult_GivenFailedValidation_MapsTheErrorValue()
	{
		// Arrange
		var validation = ValidationResult<string>.Failure(
			ValidationError.Create("too_small", "Value is too short.", ["Value"])
		);

		// Act
		var result = validation.ToResult(errors => errors.Single());

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Error.Code).IsEqualTo("too_small");
		await Assert.That(result.Error.Message).IsEqualTo("Value is too short.");
		await Assert.That(result.Error.Path).IsEquivalentTo(["Value"]);
	}

	[Test]
	public async Task ToResult_GivenFailedValidation_PreservesEveryErrorsMetadata()
	{
		// Arrange
		var first = ValidationError.Create(
			code: "invalid_provider_connection_id",
			message: "A provider connection identifier is required.",
			path: ["ProviderConnectionId"],
			category: "invalid_value"
		);
		var second = ValidationError.Create(
			code: "invalid_provider_connection_name",
			message: "A provider connection name is required.",
			path: ["ProviderConnectionName"]
		);
		var validation = ValidationResult<int>.Failure([first, second]);

		// Act
		var result = validation.ToResult(errors => errors);

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.Length).IsEqualTo(2);
		await Assert.That(result.Error[0].Code).IsEqualTo("invalid_provider_connection_id");
		await Assert.That(result.Error[0].Category).IsEqualTo("invalid_value");
		await Assert.That(result.Error[0].Path).IsEquivalentTo(["ProviderConnectionId"]);
		await Assert.That(result.Error[1].Code).IsEqualTo("invalid_provider_connection_name");
		await Assert.That(result.Error[1].Message).IsEqualTo("A provider connection name is required.");
	}

	[Test]
	public async Task ToResult_GivenFailedValidation_KeepsEveryError()
	{
		// Arrange
		var errorsSeenByFactory = 0;
		var validation = ValidationResult<int>.Failure([
			ValidationError.Create("first", "The first failure.", []),
			ValidationError.Create("second", "The second failure.", []),
		]);

		// Act
		var result = validation.ToResult(errors =>
		{
			errorsSeenByFactory = errors.Length;
			return errors.Length;
		});

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(2);
		await Assert.That(errorsSeenByFactory).IsEqualTo(2);
	}

	[Test]
	public async Task ToResult_GivenNullFailureFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var validation = ValidationResult<int>.Success(1);

		// Act & Assert
		await Assert.That(() => validation.ToResult<int, string>(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task ToUnitResult_GivenSuccessfulValidation_ReturnsAUnitSuccessWithoutInvokingTheFactory()
	{
		// Arrange
		var validation = ValidationResult<int>.Success(42);
		var factoryInvocations = 0;

		// Act
		var result = validation.ToUnitResult<int, OperationError>(errors =>
		{
			factoryInvocations++;
			return new OperationRejected(new Operation(), errors);
		});

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(factoryInvocations).IsEqualTo(0);
	}

	[Test]
	public async Task ToUnitResult_GivenFailedValidation_ReturnsAUnitFailureCarryingTheCreatedError()
	{
		// Arrange
		var validation = ValidationResult<string>.Failure(
			ValidationError.Create("too_small", "Value is too short.", ["Value"])
		);

		// Act
		var result = validation.ToUnitResult<string, OperationError>(errors => new OperationRejected(
			new Operation(),
			errors
		));

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Error is OperationRejected).IsTrue();
	}

	[Test]
	public async Task ToUnitResult_GivenNullFailureFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var validation = ValidationResult<int>.Success(1);

		// Act & Assert
		await Assert.That(() => validation.ToUnitResult<int, string>(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task ToResultAsync_GivenSuccessfulValidation_ReturnsTheValidatedValueWithoutInvokingTheFactory()
	{
		// Arrange
		var validation = ValueTask.FromResult(ValidationResult<int>.Success(42));
		var factoryInvocations = 0;

		// Act
		var result = await validation.ToResultAsync(errors =>
		{
			factoryInvocations++;
			return errors.Length;
		});

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(42);
		await Assert.That(factoryInvocations).IsEqualTo(0);
	}

	[Test]
	public async Task ToResultAsync_GivenFailedValidation_MapsTheErrorValue()
	{
		// Arrange
		var validation = ValueTask.FromResult(
			ValidationResult<string>.Failure(ValidationError.Create("too_small", "Value is too short.", ["Value"]))
		);

		// Act
		var result = await validation.ToResultAsync(errors => errors.Single());

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Error.Code).IsEqualTo("too_small");
		await Assert.That(result.Error.Message).IsEqualTo("Value is too short.");
		await Assert.That(result.Error.Path).IsEquivalentTo(["Value"]);
	}

	[Test]
	public async Task ToResultAsync_GivenFailedValidation_KeepsEveryError()
	{
		// Arrange
		var errorsSeenByFactory = 0;
		var validation = ValueTask.FromResult(
			ValidationResult<int>.Failure([
				ValidationError.Create("first", "The first failure.", []),
				ValidationError.Create("second", "The second failure.", []),
			])
		);

		// Act
		var result = await validation.ToResultAsync(errors =>
		{
			errorsSeenByFactory = errors.Length;
			return errors.Length;
		});

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(2);
		await Assert.That(errorsSeenByFactory).IsEqualTo(2);
	}

	[Test]
	public async Task ToResultAsync_GivenNullFailureFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var validation = ValueTask.FromResult(ValidationResult<int>.Success(1));

		// Act & Assert
		await Assert
			.That(async () => await validation.ToResultAsync<int, string>(null!))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task ToUnitResultAsync_GivenSuccessfulValidation_ReturnsAUnitSuccessWithoutInvokingTheFactory()
	{
		// Arrange
		var validation = ValueTask.FromResult(ValidationResult<int>.Success(42));
		var factoryInvocations = 0;

		// Act
		var result = await validation.ToUnitResultAsync<int, OperationError>(errors =>
		{
			factoryInvocations++;
			return new OperationRejected(new Operation(), errors);
		});

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(factoryInvocations).IsEqualTo(0);
	}

	[Test]
	public async Task ToUnitResultAsync_GivenFailedValidation_ReturnsAUnitFailureCarryingTheCreatedError()
	{
		// Arrange
		var validation = ValueTask.FromResult(
			ValidationResult<string>.Failure(ValidationError.Create("too_small", "Value is too short.", ["Value"]))
		);

		// Act
		var result = await validation.ToUnitResultAsync<string, OperationError>(errors => new OperationRejected(
			new Operation(),
			errors
		));

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Error is OperationRejected).IsTrue();
	}

	[Test]
	public async Task ToUnitResultAsync_GivenNullFailureFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var validation = ValueTask.FromResult(ValidationResult<int>.Success(1));

		// Act & Assert
		await Assert
			.That(async () => await validation.ToUnitResultAsync<int, string>(null!))
			.Throws<ArgumentNullException>();
	}
}
