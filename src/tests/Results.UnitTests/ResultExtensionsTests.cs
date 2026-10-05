using System.Globalization;

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

	#region Error-widening composition

	[Test]
	public async Task Bind_WhenSuccessAndBindingSucceeds_ShouldReturnBoundSuccess()
	{
		var result = Successful("123");

		var actual = result.Bind(
			value => Result<int, OtherTestError>.Success(int.Parse(value, CultureInfo.InvariantCulture)),
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(123);
	}

	[Test]
	public async Task Bind_WhenSuccessAndBindingSucceeds_ShouldNotInvokeMapError()
	{
		var result = Successful();
		var invoked = false;

		result.Bind(
			_ => Result<int, OtherTestError>.Success(0),
			_ =>
			{
				invoked = true;

				return new OtherTestError(0);
			}
		);

		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task Bind_WhenSuccessAndBindingFails_ShouldReturnBoundFailure()
	{
		var result = Successful();
		OtherTestError error = new(42);

		var actual = result.Bind(_ => Result<int, OtherTestError>.Failure(error), _ => new OtherTestError(0));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(error);
	}

	[Test]
	public async Task Bind_WhenFailure_ShouldMapErrorAndNotInvokeBinding()
	{
		var result = Failed();
		var invoked = false;

		var actual = result.Bind(
			value =>
			{
				invoked = true;

				return Result<int, OtherTestError>.Success(value.Length);
			},
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new OtherTestError(Error.Length));
	}

	[Test]
	public async Task Bind_WhenFailure_ShouldInvokeMapErrorOnce()
	{
		var result = Failed();
		var invoked = 0;

		result.Bind(
			_ => Result<int, OtherTestError>.Success(0),
			_ =>
			{
				invoked++;

				return new OtherTestError(0);
			}
		);

		await Assert.That(invoked).IsEqualTo(1);
	}

	[Test]
	public async Task Bind_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<int, OtherTestError> Act() =>
			result.Bind(value => Result<int, OtherTestError>.Success(value.Length), _ => new OtherTestError(0));

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task Bind_WhenBindIsNull_ShouldThrow()
	{
		var result = Successful();

		Result<int, OtherTestError> Act() =>
			result.Bind((Func<string, Result<int, OtherTestError>>)null!, _ => new OtherTestError(0));

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	[Test]
	public async Task Bind_WhenMapErrorIsNull_ShouldThrow()
	{
		var result = Successful();

		Result<int, OtherTestError> Act() => result.Bind(_ => Result<int, OtherTestError>.Success(0), null!);

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	[Test]
	public async Task BindAsyncWithMapError_WhenSuccessAndBindingSucceeds_ShouldReturnBoundSuccess()
	{
		var result = Successful("123");

		var actual = await result.BindAsync(
			value =>
				Task.FromResult(Result<int, OtherTestError>.Success(int.Parse(value, CultureInfo.InvariantCulture))),
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(123);
	}

	[Test]
	public async Task BindAsync_WhenSuccessAndBindingSucceeds_ShouldNotInvokeMapError()
	{
		var result = Successful();
		var invoked = false;

		await result.BindAsync(
			_ => Task.FromResult(Result<int, OtherTestError>.Success(0)),
			_ =>
			{
				invoked = true;

				return new OtherTestError(0);
			}
		);

		await Assert.That(invoked).IsFalse();
	}

	[Test]
	public async Task BindAsync_WhenFailure_ShouldMapErrorAndNotInvokeBinding()
	{
		var result = Failed();
		var invoked = false;

		var actual = await result.BindAsync(
			value =>
			{
				invoked = true;

				return Task.FromResult(Result<int, OtherTestError>.Success(value.Length));
			},
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new OtherTestError(Error.Length));
	}

	[Test]
	public async Task BindAsync_WhenUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		async Task Act() =>
			await result.BindAsync(
				value => Task.FromResult(Result<int, OtherTestError>.Success(value.Length)),
				_ => new OtherTestError(0)
			);

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task BindAsyncWithMapError_WhenBindIsNull_ShouldThrow()
	{
		var result = Successful();

		async Task Act() =>
			await result.BindAsync((Func<string, Task<Result<int, OtherTestError>>>)null!, _ => new OtherTestError(0));

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	[Test]
	public async Task BindAsync_WhenMapErrorIsNull_ShouldThrow()
	{
		var result = Successful();

		async Task Act() => await result.BindAsync(_ => Task.FromResult(Result<int, OtherTestError>.Success(0)), null!);

		await Assert.That(Act).ThrowsExactly<ArgumentNullException>();
	}

	#endregion

	#region Throwing

	[Test]
	public async Task Throw_WhenSuccess_ShouldReturnValue()
	{
		var result = Successful();

		await Assert.That(result.Throw()).IsEqualTo(Value);
	}

	[Test]
	public async Task Throw_WhenFailure_ShouldThrowResultException()
	{
		var result = Failed();

		string Act() => result.Throw();

		await Assert.That(Act).ThrowsExactly<ResultException<TestError>>();
	}

	[Test]
	public async Task Throw_WhenFailure_ShouldExposeTheError()
	{
		var result = Failed();

		var exception = ThrowAndCapture(result);

		await Assert.That(exception.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task Throw_WhenFailure_ShouldIncludeTheErrorInTheMessage()
	{
		var result = Failed();

		var exception = ThrowAndCapture(result);

		await Assert.That(exception.Message).IsEqualTo($"The result represents a failure: {Error}");
	}

	[Test]
	public async Task Throw_WhenFailure_ShouldBeCatchableAsTheNonGenericBase()
	{
		var result = Failed();
		var caught = false;

		try
		{
			result.Throw();
		}
		catch (ResultException exception)
		{
			caught = exception.Error is TestError;
		}

		await Assert.That(caught).IsTrue();
	}

	[Test]
	public async Task Throw_WhenUninitialized_ShouldThrowInvalidOperationException()
	{
		Result<string, TestError> result = default;

		string Act() => result.Throw();

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	#region Unit result extensions

	static Result<TestError> UnitSuccessful() => Result<TestError>.Success();

	static Result<TestError> UnitFailed() => Result<TestError>.Failure(new(Error));

	[Test]
	public async Task TryGetError_WhenUnitFailure_ShouldReturnTrueAndError()
	{
		var result = UnitFailed();

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsTrue();
		await Assert.That(error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task TryGetError_WhenUnitSuccess_ShouldReturnFalse()
	{
		var result = UnitSuccessful();

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsFalse();
		await Assert.That(error).IsEqualTo(default);
	}

	[Test]
	public async Task TryGetError_WhenUnitUninitialized_ShouldReturnFalse()
	{
		Result<TestError> result = default;

		var found = result.TryGetError(out var error);

		await Assert.That(found).IsFalse();
		await Assert.That(error).IsEqualTo(default);
	}

	[Test]
	public async Task GetErrorOrDefault_WhenUnitFailure_ShouldReturnError()
	{
		var result = UnitFailed();

		await Assert.That(result.GetErrorOrDefault(new TestError("fallback"))).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task GetErrorOrDefault_WhenUnitSuccess_ShouldReturnFallback()
	{
		var result = UnitSuccessful();

		await Assert.That(result.GetErrorOrDefault(new TestError("fallback"))).IsEqualTo(new TestError("fallback"));
	}

	[Test]
	public async Task OrElse_WhenUnitFailure_ShouldReturnFallback()
	{
		var result = UnitFailed();

		var actual = result.OrElse(UnitSuccessful());

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task OrElse_WhenUnitSuccess_ShouldReturnOriginal()
	{
		var result = UnitSuccessful();

		var actual = result.OrElse(UnitFailed());

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Switch_WhenUnitSuccess_ShouldInvokeSuccessOnly()
	{
		var result = UnitSuccessful();
		var successInvoked = false;
		var failureInvoked = false;

		result.Switch(() => successInvoked = true, _ => failureInvoked = true);

		await Assert.That(successInvoked).IsTrue();
		await Assert.That(failureInvoked).IsFalse();
	}

	[Test]
	public async Task Switch_WhenUnitFailure_ShouldInvokeFailureOnly()
	{
		var result = UnitFailed();
		var successInvoked = false;
		var failureInvoked = false;

		result.Switch(() => successInvoked = true, _ => failureInvoked = true);

		await Assert.That(successInvoked).IsFalse();
		await Assert.That(failureInvoked).IsTrue();
	}

	[Test]
	public async Task Tap_WhenUnitSuccess_ShouldInvokeAndReturnResult()
	{
		var result = UnitSuccessful();
		var invoked = false;

		var actual = result.Tap(() => invoked = true);

		await Assert.That(invoked).IsTrue();
		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task TapError_WhenUnitFailure_ShouldInvokeAndReturnResult()
	{
		var result = UnitFailed();
		TestError? observed = null;

		var actual = result.TapError(error => observed = error);

		await Assert.That(observed).IsEqualTo(new TestError(Error));
		await Assert.That(actual.IsFailure).IsTrue();
	}

	[Test]
	public async Task Ensure_WhenUnitPredicateFails_ShouldReturnFailureWithCreatedError()
	{
		var result = UnitSuccessful();

		var actual = result.Ensure(() => false, () => new TestError("constraint failed"));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError("constraint failed"));
	}

	[Test]
	public async Task Ensure_WhenUnitPredicateSucceeds_ShouldReturnOriginalSuccess()
	{
		var result = UnitSuccessful();

		var actual = result.Ensure(() => true, () => new TestError("constraint failed"));

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task MapAsync_WhenUnitSuccess_ShouldProduceValue()
	{
		var result = UnitSuccessful();

		var actual = await result.MapAsync(() => Task.FromResult(Value.Length));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(Value.Length);
	}

	[Test]
	public async Task MapAsync_WhenUnitFailure_ShouldPreserveError()
	{
		var result = UnitFailed();

		var actual = await result.MapAsync(() => Task.FromResult(Value.Length));

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task BindAsync_WhenUnitSuccess_ShouldReturnBoundUnitSuccess()
	{
		var result = UnitSuccessful();

		var actual = await result.BindAsync(() => Task.FromResult(UnitSuccessful()));

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task BindAsyncToValue_WhenUnitSuccess_ShouldReturnBoundValueResult()
	{
		var result = UnitSuccessful();

		var actual = await result.BindAsync(() => Task.FromResult(Result<int, TestError>.Success(123)));

		await Assert.That(actual.IsSuccess).IsTrue();
		await Assert.That(actual.Value).IsEqualTo(123);
	}

	[Test]
	public async Task BindAsync_WhenUnitFailure_ShouldPreserveErrorAndNotInvokeBinding()
	{
		var result = UnitFailed();
		var invoked = false;

		var actual = await result.BindAsync(() =>
		{
			invoked = true;

			return Task.FromResult(UnitSuccessful());
		});

		await Assert.That(invoked).IsFalse();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task Bind_WhenUnitSuccessAndBindingSucceeds_ShouldWidenAndReturnBoundUnitSuccess()
	{
		var result = UnitSuccessful();

		var actual = result.Bind(Result<OtherTestError>.Success, error => new OtherTestError(error.Message.Length));

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task BindToValue_WhenUnitFailure_ShouldMapError()
	{
		var result = UnitFailed();

		var actual = result.Bind(
			() => Result<int, OtherTestError>.Success(123),
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new OtherTestError(Error.Length));
	}

	[Test]
	public async Task BindAsyncWithMapError_WhenUnitFailure_ShouldMapError()
	{
		var result = UnitFailed();

		var actual = await result.BindAsync(
			() => Task.FromResult(Result<OtherTestError>.Success()),
			error => new OtherTestError(error.Message.Length)
		);

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new OtherTestError(Error.Length));
	}

	[Test]
	public async Task Throw_WhenUnitSuccess_ShouldReturnTheSuccessMarker()
	{
		var result = UnitSuccessful();

		await Assert.That(result.Throw()).IsEqualTo(Success.Instance);
	}

	[Test]
	public async Task Throw_WhenUnitFailure_ShouldThrowResultException()
	{
		var result = UnitFailed();

		var exception = ThrowUnitAndCapture(result);

		await Assert.That(exception.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task Throw_WhenUnitUninitialized_ShouldThrowInvalidOperationException()
	{
		Result<TestError> result = default;

		Success Act() => result.Throw();

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	[Test]
	public async Task DiscardValue_WhenValueSuccess_ShouldReturnUnitSuccess()
	{
		var result = Successful();

		var actual = result.DiscardValue();

		await Assert.That(actual.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscardValue_WhenValueFailure_ShouldPreserveError()
	{
		var result = Failed();

		var actual = result.DiscardValue();

		await Assert.That(actual.IsFailure).IsTrue();
		await Assert.That(actual.Error).IsEqualTo(new TestError(Error));
	}

	[Test]
	public async Task DiscardValue_WhenValueUninitialized_ShouldThrow()
	{
		Result<string, TestError> result = default;

		Result<TestError> Act() => result.DiscardValue();

		await Assert
			.That(Act)
			.ThrowsExactly<InvalidOperationException>()
			.WithMessage("The result is uninitialized.", StringComparison.Ordinal);
	}

	#endregion

	static ResultException<TestError> ThrowAndCapture(Result<string, TestError> result)
	{
		try
		{
			result.Throw();
		}
		catch (ResultException<TestError> exception)
		{
			return exception;
		}

		throw new InvalidOperationException("The result did not throw.");
	}

	static ResultException<TestError> ThrowUnitAndCapture(Result<TestError> result)
	{
		try
		{
			result.Throw();
		}
		catch (ResultException<TestError> exception)
		{
			return exception;
		}

		throw new InvalidOperationException("The result did not throw.");
	}

	readonly record struct TestError(string Message)
	{
		public override string ToString() => Message;
	}

	readonly record struct OtherTestError(int Code);
}
