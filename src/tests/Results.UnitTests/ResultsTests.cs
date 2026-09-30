using System.Globalization;

namespace Purview.Results;

public sealed class ResultTests
{
	const string Value = "value";
	const string Error = "error";

	#region State

	[Test]
	public async Task DefaultResult_ShouldBeUninitialized()
	{
		Result<string, string> result = default;

		await Assert.That(result.IsInitialized).IsFalse();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.IsFailure).IsFalse();
	}

	[Test]
	public async Task Success_ShouldHaveExpectedState()
	{
		var result = Result<string, TestError>.Success(Value);

		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.IsFailure).IsFalse();
		await Assert.That(result.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task Failure_ShouldHaveExpectedState()
	{
		TestError error = new(Error);

		var result = Result<string, TestError>.Failure(error);

		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Value_WhenFailure_ShouldThrow()
	{
		var result = Result<string, TestError>.Failure(new(Error));

		string act() => result.Value;

		await Assert
			.That((Func<string>)act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The value of a non-successful result cannot be accessed.", StringComparison.Ordinal);
	}

	[Test]
	public async Task Error_WhenSuccess_ShouldThrow()
	{
		var result = Result<string, TestError>.Success(Value);

		TestError act() => result.Error;

		await Assert
			.That(act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The error of a non-failed result cannot be accessed.", StringComparison.Ordinal);
	}

	[Test]
	public async Task Value_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		string act() => result.Value;

		await Assert.That((Func<string>)act).ThrowsExactly<InvalidOperationException>();
	}

	[Test]
	public async Task Error_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		TestError act() => result.Error;

		await Assert.That(act).ThrowsExactly<InvalidOperationException>();
	}

	#endregion

	#region Factories and conversions

	[Test]
	public async Task ResultSuccessFactory_ShouldCreateSuccess()
	{
		var result = Result.Success<string, TestError>(Value);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task ResultFailureFactory_ShouldCreateFailure()
	{
		TestError error = new(Error);

		var result = Result.Failure<string, TestError>(error);

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	[Test]
	public async Task ImplicitValueConversion_ShouldCreateSuccess()
	{
		Result<string, TestError> result = Value;

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task ImplicitErrorConversion_ShouldCreateFailure()
	{
		TestError error = new(Error);

		Result<string, TestError> result = error;

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsEqualTo(error);
	}

	#endregion

	#region ToString

	[Test]
	public async Task ToString_WhenSuccess_ShouldDescribeSuccess()
	{
		var result = Result<string, TestError>.Success(Value);

		await Assert.That(result.ToString()).IsEqualTo("Success(value)");
	}

	[Test]
	public async Task ToString_WhenFailure_ShouldDescribeFailure()
	{
		var result = Result<string, TestError>.Failure(new(Error));

		await Assert.That(result.ToString()).IsEqualTo("Failure(error)");
	}

	[Test]
	public async Task ToString_WhenUninitialized_ShouldDescribeUninitialized()
	{
		Result<string, TestError> result = default;

		await Assert.That(result.ToString()).IsEqualTo("Uninitialized");
	}

	#endregion

	#region Match

	[Test]
	public async Task Match_WhenSuccess_ShouldInvokeSuccess()
	{
		var result = Result<string, TestError>.Success(Value);

		var matched = result.Match(success: value => $"success:{value}", failure: error => $"failure:{error}");

		await Assert.That(matched).IsEqualTo("success:value");
	}

	[Test]
	public async Task Match_WhenFailure_ShouldInvokeFailure()
	{
		var result = Result<string, TestError>.Failure(new(Error));

		var matched = result.Match(success: value => $"success:{value}", failure: error => $"failure:{error}");

		await Assert.That(matched).IsEqualTo("failure:error");
	}

	[Test]
	public async Task Match_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		string act() => result.Match(success: value => value, failure: error => error.Message);

		await Assert
			.That((Func<string>)act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region Map

	[Test]
	public async Task Map_WhenSuccess_ShouldTransformValue()
	{
		var result = Result<string, TestError>.Success("123");

		var mapped = result.Map(int.Parse);

		await Assert.That(mapped.IsSuccess).IsTrue();
		await Assert.That(mapped.Value).IsEqualTo(123);
	}

	[Test]
	public async Task Map_WhenFailure_ShouldPreserveError()
	{
		TestError error = new(Error);
		var result = Result<string, TestError>.Failure(error);

		var mapped = result.Map(value => value.Length);

		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Map_WhenFailure_ShouldNotInvokeMappingFunction()
	{
		var result = Result<string, TestError>.Failure(new(Error));
		var invoked = false;

		result.Map(value =>
		{
			invoked = true;
			return value.Length;
		});

		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task Map_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<int, TestError> act() => result.Map(value => value.Length);

		await Assert
			.That(act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region MapError

	[Test]
	public async Task MapError_WhenFailure_ShouldTransformError()
	{
		var result = Result<string, TestError>.Failure(new(Error));

		var mapped = result.MapError(error => new OtherTestError(error.Message.Length));

		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error).IsEqualTo(new OtherTestError(5));
	}

	[Test]
	public async Task MapError_WhenSuccess_ShouldPreserveValue()
	{
		var result = Result<string, TestError>.Success(Value);

		var mapped = result.MapError(error => new OtherTestError(error.Message.Length));

		await Assert.That(mapped.IsSuccess).IsTrue();
		await Assert.That(mapped.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task MapError_WhenSuccess_ShouldNotInvokeMappingFunction()
	{
		var result = Result<string, TestError>.Success(Value);
		var invoked = false;

		result.MapError(error =>
		{
			invoked = true;
			return new OtherTestError(error.Message.Length);
		});

		await Assert.That(invoked).IsFalse();
	}

	#endregion

	#region Bind

	[Test]
	public async Task Bind_WhenSuccessAndBindingSucceeds_ShouldReturnBoundSuccess()
	{
		var result = Result<string, TestError>.Success("123");

		var bound = result.Bind(value =>
			Result<int, TestError>.Success(int.Parse(value, CultureInfo.InvariantCulture))
		);

		await Assert.That(bound.IsSuccess).IsTrue();
		await Assert.That(bound.Value).IsEqualTo(123);
	}

	[Test]
	public async Task Bind_WhenSuccessAndBindingFails_ShouldReturnBoundFailure()
	{
		var result = Result<string, TestError>.Success(Value);
		TestError error = new(Error);

		var bound = result.Bind(_ => Result<int, TestError>.Failure(error));

		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Bind_WhenFailure_ShouldPreserveError()
	{
		TestError error = new(Error);
		var result = Result<string, TestError>.Failure(error);

		var bound = result.Bind(value => Result<int, TestError>.Success(value.Length));

		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Bind_WhenFailure_ShouldNotInvokeBindingFunction()
	{
		var result = Result<string, TestError>.Failure(new(Error));
		var invoked = false;

		result.Bind(value =>
		{
			invoked = true;
			return Result<int, TestError>.Success(value.Length);
		});

		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task Bind_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<int, TestError> act() => result.Bind(value => Result<int, TestError>.Success(value.Length));

		await Assert
			.That(act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	readonly record struct TestError(string Message)
	{
		public override string ToString() => Message;
	}

	readonly record struct OtherTestError(int Code);
}
