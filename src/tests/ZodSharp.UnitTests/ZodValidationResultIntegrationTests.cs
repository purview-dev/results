namespace Purview.Results.ZodSharp;

/// <summary>
/// Integration tests that run the real ZodSharp-generated schemas of this test project and compose their outcomes
/// into results whose error is the local <see cref="OperationError"/> union.
/// </summary>
public class ZodValidationResultIntegrationTests
{
	const string InvalidOperationResultCode = "invalid_operation_result";

	[Test]
	public async Task OperationSchema_GivenMissingName_FailsWithTheMemberPath()
	{
		// Arrange
		var operation = Operation.Create(null);

		// Act
		var validation = OperationSchema.Validate(operation);

		// Assert
		await Assert.That(validation.IsSuccess).IsFalse();
		await Assert.That(validation.Errors.Length).IsGreaterThanOrEqualTo(1);
		await Assert.That(validation.Errors[0].Path).Contains(nameof(Operation.Name));
	}

	[Test]
	public async Task ExecuteGuard_GivenRejectedOperation_ReturnsTheUnionCaseAsAFailure()
	{
		// Arrange
		var operation = Operation.Create(null);

		// Act
		var result = Run(operation);

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();

		OperationRejected? rejected = result.Error switch
		{
			OperationRejected value => value,
			_ => null,
		};

		await Assert.That(rejected).IsNotNull();
		await Assert.That(rejected!.Value.Operation).IsEqualTo(operation);
		await Assert.That(rejected.Value.Errors[0].Path).Contains(nameof(Operation.Name));

		// The guard idiom: the method succeeds with a different type than the validated value, so the generated
		// AsFailure<TValue>() helper produces the failure.
		static Result<OperationResult, OperationError> Run(Operation operation)
		{
			var validation = OperationSchema.Validate(operation);
			return validation.IsSuccess
				? (Result<OperationResult, OperationError>)OperationResult.Create(1, 1, 0)
				: new OperationRejected(operation, validation.Errors).AsFailure<OperationResult>();
		}
	}

	[Test]
	public async Task ExecuteGuard_GivenValidOperation_ReturnsTheProducedAggregate()
	{
		// Arrange
		var operation = Operation.Create("reconcile");

		// Act
		var result = Run(operation);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(OperationResult.Create(1, 1, 0));

		static Result<OperationResult, OperationError> Run(Operation operation)
		{
			var validation = OperationSchema.Validate(operation);
			return validation.IsSuccess
				? (Result<OperationResult, OperationError>)OperationResult.Create(1, 1, 0)
				: new OperationRejected(operation, validation.Errors).AsFailure<OperationResult>();
		}
	}

	[Test]
	public async Task ToResult_GivenValidOperation_SucceedsWithTheValidatedValueWithoutInvokingTheFactory()
	{
		// Arrange
		var operation = Operation.Create("reconcile");
		var factoryInvocations = 0;

		// Act
		var result = OperationSchema
			.Validate(operation)
			.ToResult<Operation, OperationError>(errors =>
			{
				factoryInvocations++;
				return new OperationRejected(operation, errors);
			});

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(operation);
		await Assert.That(factoryInvocations).IsEqualTo(0);
	}

	[Test]
	public async Task ToResult_GivenRejectedOperation_DoesNotInvokeTheMappingOrTheBinder()
	{
		// Arrange
		var operation = Operation.Create(null);
		var bindInvocations = 0;
		var mapInvocations = 0;

		// Act
		var result = OperationSchema
			.Validate(operation)
			.ToResult<Operation, OperationError>(errors => new OperationRejected(operation, errors))
			.Map(value =>
			{
				mapInvocations++;
				return value;
			})
			.Bind(value =>
			{
				bindInvocations++;
				return Result<Operation, OperationError>.Success(value);
			});

		// Assert
		await Assert.That(mapInvocations).IsEqualTo(0);
		await Assert.That(bindInvocations).IsEqualTo(0);
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is OperationRejected).IsTrue();
	}

	[Test]
	public async Task OperationResultSchema_GivenInconsistentTotals_FailsTheAggregateInvariant()
	{
		// Arrange
		// Discovered (3) does not equal Completed + Skipped (2).
		var operationResult = OperationResult.Create(discovered: 3, completed: 1, skipped: 1);

		// Act
		var validation = OperationResultSchema.Validate(operationResult);

		// Assert
		await Assert.That(validation.IsSuccess).IsFalse();
		await Assert.That(validation.Errors.Select(static error => error.Code)).Contains(InvalidOperationResultCode);
	}

	[Test]
	public async Task ToResult_GivenInconsistentAggregate_ProducesAFailureCarryingTheAggregateAndItsErrors()
	{
		// Arrange
		var operationResult = OperationResult.Create(discovered: 3, completed: 1, skipped: 1);

		// Act
		// The validated value is the success type, so the adapter maps the failure into the union directly.
		var result = OperationResultSchema
			.Validate(operationResult)
			.ToResult<OperationResult, OperationError>(errors => new OperationResultInvalid(operationResult, errors));

		// Assert
		await Assert.That(result.IsFailure).IsTrue();

		OperationResultInvalid? invalid = result.Error switch
		{
			OperationResultInvalid value => value,
			_ => null,
		};

		await Assert.That(invalid).IsNotNull();
		await Assert.That(invalid!.Value.Result).IsEqualTo(operationResult);
		await Assert.That(invalid.Value.Errors[0].Code).IsEqualTo(InvalidOperationResultCode);
	}

	[Test]
	public async Task ToResult_GivenConsistentAggregate_SucceedsWithTheValidatedAggregate()
	{
		// Arrange
		var operationResult = OperationResult.Create(discovered: 3, completed: 1, skipped: 2);

		// Act
		var result = OperationResultSchema
			.Validate(operationResult)
			.ToResult<OperationResult, OperationError>(errors => new OperationResultInvalid(operationResult, errors));

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(operationResult);
	}
}
