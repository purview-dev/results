namespace Purview.Results;

public sealed class ResultTErrorTests
{
	const string Error = "error";

	#region State

	[Test]
	public async Task DefaultResult_ShouldBeUninitialized()
	{
		Result<TestError> result = default;

		await Assert.That(result.IsInitialized).IsFalse();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.IsFailure).IsFalse();
	}

	[Test]
	public async Task Success_ShouldHaveExpectedState()
	{
		var result = Result<TestError>.Success();

		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.IsFailure).IsFalse();
	}

	[Test]
	public async Task Failure_ShouldHaveExpectedState()
	{
		TestError error = new(Error);

		var result = Result<TestError>.Failure(error);

		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Error_WhenSuccess_ShouldThrow()
	{
		var result = Result<TestError>.Success();

		TestError Act() => result.Error;

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The error of a non-failed result cannot be accessed.", StringComparison.Ordinal);
	}

	[Test]
	public async Task Error_WhenUninitialized_ShouldThrow()
	{
		Result<TestError> result = default;

		TestError Act() => result.Error;

		await Assert.That(Act).ThrowsExactly<InvalidOperationException>();
	}

	#endregion

	#region Factories and conversions

	[Test]
	public async Task ResultSuccessFactory_ShouldCreateSuccess()
	{
		var result = Result.Success<TestError>();

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ResultFailureFactory_ShouldCreateFailure()
	{
		TestError error = new(Error);

		var result = Result.Failure(error);

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	[Test]
	public async Task ImplicitSuccessConversion_ShouldCreateSuccess()
	{
		Result<TestError> result = Success.Instance;

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ImplicitErrorConversion_ShouldCreateFailure()
	{
		TestError error = new(Error);

		Result<TestError> result = error;

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	#endregion

	#region IResultValue

	[Test]
	public async Task IResultValue_WhenSuccess_ShouldExposeTheSuccessMarker()
	{
		IResultValue result = Result<TestError>.Success();

		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.SuccessValue).IsEqualTo(Success.Instance);
		await Assert.That(result.ErrorValue).IsNull();
	}

	[Test]
	public async Task IResultValue_WhenFailure_ShouldExposeTheError()
	{
		TestError error = new(Error);
		IResultValue result = Result<TestError>.Failure(error);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.SuccessValue).IsNull();
		await Assert.That(result.ErrorValue).IsEqualTo(error);
	}

	[Test]
	public async Task IResultValue_WhenUninitialized_ShouldExposeNeitherState()
	{
		IResultValue result = default(Result<TestError>);

		await Assert.That(result.IsInitialized).IsFalse();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.SuccessValue).IsNull();
		await Assert.That(result.ErrorValue).IsNull();
	}

	#endregion

	#region ToString

	[Test]
	public async Task ToString_WhenSuccess_ShouldDescribeSuccess()
	{
		var result = Result<TestError>.Success();

		await Assert.That(result.ToString()).IsEqualTo("Success");
	}

	[Test]
	public async Task ToString_WhenFailure_ShouldDescribeFailure()
	{
		var result = Result<TestError>.Failure(new(Error));

		await Assert.That(result.ToString()).IsEqualTo("Failure(error)");
	}

	[Test]
	public async Task ToString_WhenUninitialized_ShouldDescribeUninitialized()
	{
		Result<TestError> result = default;

		await Assert.That(result.ToString()).IsEqualTo("Uninitialized");
	}

	#endregion

	#region Match

	[Test]
	public async Task Match_WhenSuccess_ShouldInvokeSuccess()
	{
		var result = Result<TestError>.Success();

		var matched = result.Match(success: () => "success", failure: error => $"failure:{error}");

		await Assert.That(matched).IsEqualTo("success");
	}

	[Test]
	public async Task Match_WhenFailure_ShouldInvokeFailure()
	{
		var result = Result<TestError>.Failure(new(Error));

		var matched = result.Match(success: () => "success", failure: error => $"failure:{error}");

		await Assert.That(matched).IsEqualTo("failure:error");
	}

	[Test]
	public async Task Match_WhenUninitialized_ShouldThrow()
	{
		Result<TestError> result = default;

		string Act() => result.Match(success: () => "success", failure: error => error.Message);

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region Map

	[Test]
	public async Task Map_WhenSuccess_ShouldProduceValue()
	{
		var result = Result<TestError>.Success();

		var mapped = result.Map(() => 123);

		await Assert.That(mapped.IsSuccess).IsTrue();
		await Assert.That(mapped.Value).IsEqualTo(123);
	}

	[Test]
	public async Task Map_WhenFailure_ShouldPreserveErrorAndNotInvokeMapping()
	{
		TestError error = new(Error);
		var result = Result<TestError>.Failure(error);
		var invoked = false;

		var mapped = result.Map(() =>
		{
			invoked = true;

			return 123;
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Map_WhenUninitialized_ShouldThrow()
	{
		Result<TestError> result = default;

		Result<int, TestError> Act() => result.Map(() => 123);

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region MapError

	[Test]
	public async Task MapError_WhenFailure_ShouldTransformError()
	{
		var result = Result<TestError>.Failure(new(Error));

		var mapped = result.MapError(error => new OtherTestError(error.Message.Length));

		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error).IsEqualTo(new OtherTestError(5));
	}

	[Test]
	public async Task MapError_WhenSuccess_ShouldPreserveSuccess()
	{
		var result = Result<TestError>.Success();

		var mapped = result.MapError(error => new OtherTestError(error.Message.Length));

		await Assert.That(mapped.IsSuccess).IsTrue();
	}

	#endregion

	#region Bind

	[Test]
	public async Task Bind_WhenSuccessAndBindingSucceeds_ShouldReturnBoundUnitSuccess()
	{
		var result = Result<TestError>.Success();

		var bound = result.Bind(Result<TestError>.Success);

		await Assert.That(bound.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Bind_WhenSuccessAndBindingFails_ShouldReturnBoundFailure()
	{
		var result = Result<TestError>.Success();
		TestError error = new(Error);

		var bound = result.Bind(() => Result<TestError>.Failure(error));

		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Bind_WhenFailure_ShouldPreserveErrorAndNotInvokeBinding()
	{
		TestError error = new(Error);
		var result = Result<TestError>.Failure(error);
		var invoked = false;

		var bound = result.Bind(() =>
		{
			invoked = true;

			return Result<TestError>.Success();
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(bound.Error).IsEqualTo(error);
	}

	[Test]
	public async Task BindToValue_WhenSuccess_ShouldReturnBoundValueResult()
	{
		var result = Result<TestError>.Success();

		var bound = result.Bind(() => Result<int, TestError>.Success(123));

		await Assert.That(bound.IsSuccess).IsTrue();
		await Assert.That(bound.Value).IsEqualTo(123);
	}

	[Test]
	public async Task BindToValue_WhenFailure_ShouldPreserveError()
	{
		TestError error = new(Error);
		var result = Result<TestError>.Failure(error);

		var bound = result.Bind(() => Result<int, TestError>.Success(123));

		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error).IsEqualTo(error);
	}

	#endregion

	readonly record struct TestError(string Message)
	{
		public override string ToString() => Message;
	}

	readonly record struct OtherTestError(int Code);
}
