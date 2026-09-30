namespace Purview.Results;

public sealed class ResultExtensionsTests
{
	const string Value = "value";
	const string Error = "error";

	static Result<string, TestError> Successful(string value = Value) => Result<string, TestError>.Success(value);

	static Result<string, TestError> Failed() => Result<string, TestError>.Failure(new(Error));

	#region Probing

	[Test]
	public async Task TryGetValue_WhenSuccess_ShouldReturnTrueAndValue()
	{
		var result = Successful();

		var found = result.TryGetValue(out var value);

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo(Value);
	}

	[Test]
	public async Task TryGetValue_WhenFailure_ShouldReturnFalse()
	{
		var result = Failed();

		var found = result.TryGetValue(out var value);

		await Assert.That(found).IsFalse();
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task TryGetValue_WhenUninitialized_ShouldReturnFalse()
	{
		Result<string, TestError> result = default;

		var found = result.TryGetValue(out var value);

		await Assert.That(found).IsFalse();
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task TryGetError_WhenFailure_ShouldReturnTrueAndError()
	{
		var result = Failed();

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsTrue();
		await Assert.That(error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task TryGetError_WhenSuccess_ShouldReturnFalse()
	{
		var result = Successful();

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsFalse();
		await Assert.That(error).IsEqualTo(default);
	}

	[Test]
	public async Task TryGetError_WhenUninitialized_ShouldReturnFalse()
	{
		Result<string, TestError> result = default;

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsFalse();
		await Assert.That(error).IsEqualTo(default);
	}

	#endregion

	#region Fallbacks

	[Test]
	public async Task GetValueOrDefault_WhenSuccess_ShouldReturnValue()
	{
		var result = Successful();

		await Assert.That(result.GetValueOrDefault()).IsEqualTo(Value);
	}

	[Test]
	public async Task GetValueOrDefault_WhenFailure_ShouldReturnDefault()
	{
		var result = Failed();

		await Assert.That(result.GetValueOrDefault() is null).IsTrue();
	}

	[Test]
	public async Task GetValueOrDefault_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		string Act() => result.GetValueOrDefault();

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task GetValueOrDefaultWithFallback_WhenFailure_ShouldReturnFallback()
	{
		var result = Failed();

		await Assert.That(result.GetValueOrDefault("fallback")).IsEqualTo("fallback");
	}

	[Test]
	public async Task GetValueOrDefaultWithFactory_WhenFailure_ShouldReturnProducedValue()
	{
		var result = Failed();

		await Assert.That(result.GetValueOrDefault(() => "produced")).IsEqualTo("produced");
	}

	[Test]
	public async Task GetValueOrDefaultWithFactory_WhenSuccess_ShouldNotInvokeFactory()
	{
		var result = Successful();
		var invoked = false;

		var actual = result.GetValueOrDefault(() =>
		{
			invoked = true;

			return "produced";
		});

		await Assert.That(actual).IsEqualTo(Value);
		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task GetValueOrDefaultWithFactory_WhenFactoryIsNull_ShouldThrow()
	{
		var result = Failed();

		string Act() => result.GetValueOrDefault((Func<string>)null!);

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	[Test]
	public async Task GetErrorOrDefault_WhenFailure_ShouldReturnError()
	{
		var result = Failed();

		await Assert.That(result.GetErrorOrDefault(new TestError("fallback"))).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task GetErrorOrDefault_WhenSuccess_ShouldReturnFallback()
	{
		var result = Successful();

		await Assert.That(result.GetErrorOrDefault(new TestError("fallback"))).IsEqualTo(new TestError("fallback"));
	}

	[Test]
	public async Task OrElse_WhenFailure_ShouldReturnFallback()
	{
		var result = Failed();

		var actual = result.OrElse(Successful("fallback"));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo("fallback");
	}

	[Test]
	public async Task OrElse_WhenSuccess_ShouldReturnOriginal()
	{
		var result = Successful();

		var actual = result.OrElse(Successful("fallback"));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task OrElse_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<string, TestError> Act() => result.OrElse(Successful());

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region Side effects

	[Test]
	public async Task Switch_WhenSuccess_ShouldInvokeSuccessOnly()
	{
		var result = Successful();
		var successInvoked = false;
		var failureInvoked = false;

		result.Switch(_ => successInvoked = true, _ => failureInvoked = true);

		await Assert.That(successInvoked).IsTrue();
		await Assert.That(failureInvoked).IsFalse();
	}

	[Test]
	public async Task Switch_WhenFailure_ShouldInvokeFailureOnly()
	{
		var result = Failed();
		var successInvoked = false;
		var failureInvoked = false;

		result.Switch(_ => successInvoked = true, _ => failureInvoked = true);

		await Assert.That(successInvoked).IsFalse();
		await Assert.That(failureInvoked).IsTrue();
	}

	[Test]
	public async Task Switch_WhenSuccessFunctionIsNull_ShouldThrow()
	{
		var result = Successful();

		void Act() => result.Switch(null!, _ => { });

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	[Test]
	public async Task Switch_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		void Act() => result.Switch(_ => { }, _ => { });

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task Tap_WhenSuccess_ShouldInvokeAndReturnResult()
	{
		var result = Successful();
		string? observed = null;

		var actual = result.Tap(value => observed = value);

		await Assert.That(observed).IsEqualTo(Value);
		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task Tap_WhenFailure_ShouldNotInvoke()
	{
		var result = Failed();
		var invoked = false;

		var actual = result.Tap(_ => invoked = true);

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsFailure).IsTrue();
	}

	[Test]
	public async Task TapError_WhenFailure_ShouldInvokeAndReturnResult()
	{
		var result = Failed();
		TestError? observed = null;

		var actual = result.TapError(error => observed = error);

		await Assert.That(observed).IsEqualTo(new TestError(Error));
		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task TapError_WhenSuccess_ShouldNotInvoke()
	{
		var result = Successful();
		var invoked = false;

		var actual = result.TapError(_ => invoked = true);

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Tap_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<string, TestError> Act() => result.Tap(_ => { });

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region Constraints

	[Test]
	public async Task Ensure_WhenPredicateFails_ShouldReturnFailureWithCreatedError()
	{
		var result = Successful();

		var actual = result.Ensure(value => value.Length > 10, value => new TestError($"too short: {value}"));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError($"too short: {Value}"));
	}

	[Test]
	public async Task Ensure_WhenPredicateSucceeds_ShouldReturnOriginalSuccess()
	{
		var result = Successful();

		var actual = result.Ensure(value => value.Length > 1, value => new TestError(value));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value);
	}

	[Test]
	public async Task Ensure_WhenFailure_ShouldPreserveErrorAndNotInvokePredicate()
	{
		var result = Failed();
		var invoked = false;

		var actual = result.Ensure(
			_ =>
			{
				invoked = true;

				return true;
			},
			value => new TestError(value)
		);

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task Ensure_WhenPredicateIsNull_ShouldThrow()
	{
		var result = Successful();

		Result<string, TestError> Act() => result.Ensure(null!, value => new TestError(value));

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	#endregion

	#region Asynchronous composition

	[Test]
	public async Task MapAsync_WhenSuccess_ShouldMapValue()
	{
		var result = Successful();

		var actual = await result.MapAsync(value => Task.FromResult(value.Length));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value.Length);
	}

	[Test]
	public async Task MapAsync_WhenFailure_ShouldPreserveError()
	{
		var result = Failed();

		var actual = await result.MapAsync(value => Task.FromResult(value.Length));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task MapAsync_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		async Task Act() => await result.MapAsync(value => Task.FromResult(value.Length));

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task BindAsync_WhenSuccessAndBindingSucceeds_ShouldReturnBoundSuccess()
	{
		var result = Successful();

		var actual = await result.BindAsync(value => Task.FromResult(Result<int, TestError>.Success(value.Length)));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value.Length);
	}

	[Test]
	public async Task BindAsync_WhenSuccessAndBindingFails_ShouldReturnBoundFailure()
	{
		var result = Successful();
		TestError error = new(Error);

		var actual = await result.BindAsync(_ => Task.FromResult(Result<int, TestError>.Failure(error)));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(error);
	}

	[Test]
	public async Task BindAsync_WhenFailure_ShouldPreserveErrorAndNotInvokeBinding()
	{
		var result = Failed();
		var invoked = false;

		var actual = await result.BindAsync(_ =>
		{
			invoked = true;

			return Task.FromResult(Result<int, TestError>.Success(0));
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task BindAsync_WhenBindIsNull_ShouldThrow()
	{
		var result = Successful();

		async Task Act() => await result.BindAsync((Func<string, Task<Result<int, TestError>>>)null!);

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	#endregion

	readonly record struct TestError(string Message)
	{
		public override string ToString() => Message;
	}
}
