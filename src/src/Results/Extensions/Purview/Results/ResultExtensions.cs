using System.Diagnostics.CodeAnalysis;

namespace Purview.Results;

/// <summary>
/// Provides value-style operations over a <see cref="Result{TValue, TError}"/> without widening the type
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// Every operation that depends on the result's state throws <see cref="InvalidOperationException"/> when the
/// result is uninitialized (<c>default</c>), matching the members on <see cref="Result{TValue, TError}"/>: an
/// uninitialized result is a bug, never a success and never a failure.
/// </para>
/// <para>
/// <see cref="TryGetValue{TValue, TError}(Result{TValue, TError}, out TValue)"/> and
/// <see cref="TryGetError{TValue, TError}(Result{TValue, TError}, out TError)"/> are the deliberate exception:
/// they report whether a value or an error is available and never throw, so a caller can inspect a result it
/// did not create.
/// </para>
/// </remarks>
public static class ResultExtensions
{
	extension<TValue, TError>(Result<TValue, TError> result)
	{
		#region Probing

		/// <summary>
		/// Attempts to read the successful value without throwing.
		/// </summary>
		/// <param name="value">
		/// The successful value when the result represents success; otherwise <see langword="default"/>.
		/// </param>
		/// <returns><see langword="true"/> when the result represents success; otherwise <see langword="false"/>.</returns>
		public bool TryGetValue([NotNullWhen(true)] out TValue value)
		{
			if (result.IsSuccess)
			{
				value = result.Value;

				return true;
			}

			value = default!;

			return false;
		}

		/// <summary>
		/// Attempts to read the error without throwing.
		/// </summary>
		/// <param name="error">
		/// The error when the result represents failure; otherwise <see langword="default"/>.
		/// </param>
		/// <returns><see langword="true"/> when the result represents failure; otherwise <see langword="false"/>.</returns>
		public bool TryGetError([NotNullWhen(true)] out TError error)
		{
			if (result.IsFailure)
			{
				error = result.Error;

				return true;
			}

			error = default!;

			return false;
		}

		#endregion

		#region Fallbacks

		/// <summary>
		/// Gets the successful value, or <see langword="default"/> when the result represents failure.
		/// </summary>
		/// <returns>The successful value, or <see langword="default"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TValue GetValueOrDefault()
		{
			RequireInitialized(result);

			return result.IsSuccess ? result.Value : default!;
		}

		/// <summary>
		/// Gets the successful value, or <paramref name="fallback"/> when the result represents failure.
		/// </summary>
		/// <param name="fallback">The value to return when the result represents failure.</param>
		/// <returns>The successful value, or <paramref name="fallback"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TValue GetValueOrDefault(TValue fallback)
		{
			RequireInitialized(result);

			return result.IsSuccess ? result.Value : fallback;
		}

		/// <summary>
		/// Gets the successful value, or the value produced by <paramref name="fallbackFactory"/> when the result
		/// represents failure.
		/// </summary>
		/// <param name="fallbackFactory">
		/// Produces the value to return when the result represents failure. Only invoked for a failure, so a
		/// successful result allocates nothing extra.
		/// </param>
		/// <returns>The successful value, or the value produced by <paramref name="fallbackFactory"/>.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackFactory"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TValue GetValueOrDefault(Func<TValue> fallbackFactory)
		{
			ArgumentNullException.ThrowIfNull(fallbackFactory);
			RequireInitialized(result);

			return result.IsSuccess ? result.Value : fallbackFactory();
		}

		/// <summary>
		/// Gets the error, or <paramref name="fallback"/> when the result represents success.
		/// </summary>
		/// <param name="fallback">The error to return when the result represents success.</param>
		/// <returns>The error, or <paramref name="fallback"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TError GetErrorOrDefault(TError fallback)
		{
			RequireInitialized(result);

			return result.IsFailure ? result.Error : fallback;
		}

		/// <summary>
		/// Returns this result when it represents success, or <paramref name="fallback"/> when it represents
		/// failure.
		/// </summary>
		/// <param name="fallback">The result to return when this result represents failure.</param>
		/// <returns>This result, or <paramref name="fallback"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TValue, TError> OrElse(Result<TValue, TError> fallback)
		{
			RequireInitialized(result);

			return result.IsSuccess ? result : fallback;
		}

		#endregion

		#region Side effects

		/// <summary>
		/// Invokes the function matching the result's current state and discards its value.
		/// </summary>
		/// <param name="success">Invoked with the successful value when the result represents success.</param>
		/// <param name="failure">Invoked with the error when the result represents failure.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="success"/> or <paramref name="failure"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public void Switch(Action<TValue> success, Action<TError> failure)
		{
			ArgumentNullException.ThrowIfNull(success);
			ArgumentNullException.ThrowIfNull(failure);
			RequireInitialized(result);

			if (result.IsSuccess)
				success(result.Value);
			else
				failure(result.Error);
		}

		/// <summary>
		/// Invokes <paramref name="success"/> with the successful value and returns the result unchanged.
		/// </summary>
		/// <param name="success">Invoked with the successful value when the result represents success.</param>
		/// <returns>This result.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="success"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TValue, TError> Tap(Action<TValue> success)
		{
			ArgumentNullException.ThrowIfNull(success);
			RequireInitialized(result);

			if (result.IsSuccess)
				success(result.Value);

			return result;
		}

		/// <summary>
		/// Invokes <paramref name="failure"/> with the error and returns the result unchanged.
		/// </summary>
		/// <param name="failure">Invoked with the error when the result represents failure.</param>
		/// <returns>This result.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="failure"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TValue, TError> TapError(Action<TError> failure)
		{
			ArgumentNullException.ThrowIfNull(failure);
			RequireInitialized(result);

			if (result.IsFailure)
				failure(result.Error);

			return result;
		}

		#endregion

		#region Constraints

		/// <summary>
		/// Turns a successful value that fails <paramref name="predicate"/> into a failure carrying the error
		/// produced by <paramref name="errorFactory"/>, and leaves a failure unchanged.
		/// </summary>
		/// <param name="predicate">Tests the successful value.</param>
		/// <param name="errorFactory">
		/// Creates the error for a value that fails the predicate. Only invoked when the predicate fails, so a
		/// satisfied constraint allocates no error.
		/// </param>
		/// <returns>This result, or the failure created by <paramref name="errorFactory"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="predicate"/> or <paramref name="errorFactory"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TValue, TError> Ensure(Func<TValue, bool> predicate, Func<TValue, TError> errorFactory)
		{
			ArgumentNullException.ThrowIfNull(predicate);
			ArgumentNullException.ThrowIfNull(errorFactory);
			RequireInitialized(result);

			if (result.IsFailure)
				return result;

			var value = result.Value;

			return predicate(value) ? result : Result<TValue, TError>.Failure(errorFactory(value));
		}

		#endregion

		#region Value discarding

		/// <summary>
		/// Discards the successful value, turning this result into a unit result that carries only the error.
		/// </summary>
		/// <returns>
		/// A successful unit result when this result represents success; otherwise, a failed unit result
		/// containing the existing error.
		/// </returns>
		/// <remarks>
		/// Use this at the point where the value is no longer needed but the failure still has to flow on — a
		/// validation step whose validated value the caller already holds, for example. It is the inverse of the
		/// unit result's <c>Map</c>, which produces a value from a success that had none.
		/// </remarks>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TError> DiscardValue()
		{
			RequireInitialized(result);

			return result.IsSuccess ? Result<TError>.Success() : Result<TError>.Failure(result.Error);
		}

		#endregion

		#region Asynchronous composition

		/// <summary>
		/// Transforms the successful value with an asynchronous function, preserving the error.
		/// </summary>
		/// <typeparam name="TResult">The type of the mapped successful value.</typeparam>
		/// <param name="map">The asynchronous function used to transform the successful value.</param>
		/// <returns>
		/// A successful result containing the mapped value when this result represents success; otherwise, a
		/// failed result containing the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public async Task<Result<TResult, TError>> MapAsync<TResult>(Func<TValue, Task<TResult>> map)
		{
			ArgumentNullException.ThrowIfNull(map);
			RequireInitialized(result);

			return result.IsSuccess
				? Result<TResult, TError>.Success(await map(result.Value))
				: Result<TResult, TError>.Failure(result.Error);
		}

		/// <summary>
		/// Binds the successful value to an asynchronous operation that can itself succeed or fail, while
		/// preserving the error type.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <param name="bind">The asynchronous function to invoke with the successful value.</param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result containing the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="bind"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TResult, TError>> BindAsync<TResult>(Func<TValue, Task<Result<TResult, TError>>> bind)
		{
			ArgumentNullException.ThrowIfNull(bind);
			RequireInitialized(result);

			return result.IsSuccess
				? bind(result.Value)
				: Task.FromResult(Result<TResult, TError>.Failure(result.Error));
		}

		#endregion

		#region Error-widening composition

		/// <summary>
		/// Binds the successful value to another operation that can itself succeed or fail, widening the existing
		/// error to the bound operation's error type when this result represents failure.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The function to invoke with the successful value.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a failed
		/// result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <remarks>
		/// Use this overload when the next step fails with a different error type — typically a union that includes
		/// this result's error. It is equivalent to <c>MapError(mapError).Bind(bind)</c> in one call.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TResult, TNewError> Bind<TResult, TNewError>(
			Func<TValue, Result<TResult, TNewError>> bind,
			Func<TError, TNewError> mapError
		)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess ? bind(result.Value) : Result<TResult, TNewError>.Failure(mapError(result.Error));
		}

		/// <summary>
		/// Binds the successful value to an asynchronous operation that can itself succeed or fail, widening the
		/// existing error to the bound operation's error type when this result represents failure.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The asynchronous function to invoke with the successful value.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a failed
		/// result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <remarks>
		/// The asynchronous counterpart of the widening <c>Bind</c> overload. It is equivalent to
		/// <c>MapError(mapError).BindAsync(bind)</c> in one call.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TResult, TNewError>> BindAsync<TResult, TNewError>(
			Func<TValue, Task<Result<TResult, TNewError>>> bind,
			Func<TError, TNewError> mapError
		)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess
				? bind(result.Value)
				: Task.FromResult(Result<TResult, TNewError>.Failure(mapError(result.Error)));
		}

		#endregion

		#region Throwing

		/// <summary>
		/// Returns the successful value, or throws a <see cref="ResultException{TError}"/> carrying the error when
		/// this result represents failure.
		/// </summary>
		/// <returns>The successful value.</returns>
		/// <remarks>
		/// This is the deliberate result-to-exception escape hatch: reach for it at a boundary that must throw, or
		/// in a test, rather than to handle an expected failure, which is normally a value. An uninitialized result
		/// is a bug, so it is reported with <see cref="InvalidOperationException"/> rather than
		/// <see cref="ResultException{TError}"/>.
		/// </remarks>
		/// <exception cref="ResultException{TError}">
		/// The result represents failure; the thrown exception exposes the error through
		/// <see cref="ResultException{TError}.Error"/>.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TValue Throw()
		{
			RequireInitialized(result);

			return result.IsSuccess ? result.Value : throw new ResultException<TError>(result.Error);
		}

		#endregion
	}

	extension<TError>(Result<TError> result)
	{
		#region Probing

		/// <summary>
		/// Attempts to read the error without throwing.
		/// </summary>
		/// <param name="error">
		/// The error when the result represents failure; otherwise <see langword="default"/>.
		/// </param>
		/// <returns><see langword="true"/> when the result represents failure; otherwise <see langword="false"/>.</returns>
		public bool TryGetError([NotNullWhen(true)] out TError error)
		{
			if (result.IsFailure)
			{
				error = result.Error;

				return true;
			}

			error = default!;

			return false;
		}

		#endregion

		#region Fallbacks

		/// <summary>
		/// Gets the error, or <paramref name="fallback"/> when the result represents success.
		/// </summary>
		/// <param name="fallback">The error to return when the result represents success.</param>
		/// <returns>The error, or <paramref name="fallback"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public TError GetErrorOrDefault(TError fallback)
		{
			RequireInitialized(result);

			return result.IsFailure ? result.Error : fallback;
		}

		/// <summary>
		/// Returns this result when it represents success, or <paramref name="fallback"/> when it represents
		/// failure.
		/// </summary>
		/// <param name="fallback">The result to return when this result represents failure.</param>
		/// <returns>This result, or <paramref name="fallback"/>.</returns>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TError> OrElse(Result<TError> fallback)
		{
			RequireInitialized(result);

			return result.IsSuccess ? result : fallback;
		}

		#endregion

		#region Side effects

		/// <summary>
		/// Invokes the function matching the result's current state and discards its value.
		/// </summary>
		/// <param name="success">Invoked when the result represents success.</param>
		/// <param name="failure">Invoked with the error when the result represents failure.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="success"/> or <paramref name="failure"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public void Switch(Action success, Action<TError> failure)
		{
			ArgumentNullException.ThrowIfNull(success);
			ArgumentNullException.ThrowIfNull(failure);
			RequireInitialized(result);

			if (result.IsSuccess)
				success();
			else
				failure(result.Error);
		}

		/// <summary>
		/// Invokes <paramref name="success"/> and returns the result unchanged.
		/// </summary>
		/// <param name="success">Invoked when the result represents success.</param>
		/// <returns>This result.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="success"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TError> Tap(Action success)
		{
			ArgumentNullException.ThrowIfNull(success);
			RequireInitialized(result);

			if (result.IsSuccess)
				success();

			return result;
		}

		/// <summary>
		/// Invokes <paramref name="failure"/> with the error and returns the result unchanged.
		/// </summary>
		/// <param name="failure">Invoked with the error when the result represents failure.</param>
		/// <returns>This result.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="failure"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TError> TapError(Action<TError> failure)
		{
			ArgumentNullException.ThrowIfNull(failure);
			RequireInitialized(result);

			if (result.IsFailure)
				failure(result.Error);

			return result;
		}

		#endregion

		#region Constraints

		/// <summary>
		/// Turns a success that fails <paramref name="predicate"/> into a failure carrying the error produced by
		/// <paramref name="errorFactory"/>, and leaves a failure unchanged.
		/// </summary>
		/// <param name="predicate">Tests the successful outcome.</param>
		/// <param name="errorFactory">
		/// Creates the error when the predicate fails. Only invoked when the predicate fails, so a satisfied
		/// constraint allocates no error.
		/// </param>
		/// <returns>This result, or the failure created by <paramref name="errorFactory"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="predicate"/> or <paramref name="errorFactory"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TError> Ensure(Func<bool> predicate, Func<TError> errorFactory)
		{
			ArgumentNullException.ThrowIfNull(predicate);
			ArgumentNullException.ThrowIfNull(errorFactory);
			RequireInitialized(result);

			if (result.IsFailure)
				return result;

			// The predicate is only invoked for a success, so a satisfied constraint allocates nothing extra.
			return predicate() ? result : Result<TError>.Failure(errorFactory());
		}

		#endregion

		#region Asynchronous composition

		/// <summary>
		/// Produces the successful value with an asynchronous function, preserving the error.
		/// </summary>
		/// <typeparam name="TResult">The type of the produced successful value.</typeparam>
		/// <param name="map">The asynchronous function used to produce the successful value.</param>
		/// <returns>
		/// A successful result containing the produced value when this result represents success; otherwise, a
		/// failed result containing the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public async Task<Result<TResult, TError>> MapAsync<TResult>(Func<Task<TResult>> map)
		{
			ArgumentNullException.ThrowIfNull(map);
			RequireInitialized(result);

			return result.IsSuccess
				? Result<TResult, TError>.Success(await map())
				: Result<TResult, TError>.Failure(result.Error);
		}

		/// <summary>
		/// Binds the successful outcome to an asynchronous unit operation that can itself succeed or fail, while
		/// preserving the error type.
		/// </summary>
		/// <param name="bind">The asynchronous function to invoke when this result represents success.</param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result containing the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="bind"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TError>> BindAsync(Func<Task<Result<TError>>> bind)
		{
			ArgumentNullException.ThrowIfNull(bind);
			RequireInitialized(result);

			return result.IsSuccess ? bind() : Task.FromResult(Result<TError>.Failure(result.Error));
		}

		/// <summary>
		/// Binds the successful outcome to an asynchronous operation that produces a value and can itself succeed
		/// or fail, while preserving the error type.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <param name="bind">The asynchronous function to invoke when this result represents success.</param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result containing the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="bind"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TResult, TError>> BindAsync<TResult>(Func<Task<Result<TResult, TError>>> bind)
		{
			ArgumentNullException.ThrowIfNull(bind);
			RequireInitialized(result);

			return result.IsSuccess ? bind() : Task.FromResult(Result<TResult, TError>.Failure(result.Error));
		}

		#endregion

		#region Error-widening composition

		/// <summary>
		/// Binds the successful outcome to another unit operation that can itself succeed or fail, widening the
		/// existing error to the bound operation's error type when this result represents failure.
		/// </summary>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The function to invoke when this result represents success.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <remarks>
		/// Use this overload when the next step fails with a different error type — typically a union that
		/// includes this result's error. It is equivalent to <c>MapError(mapError).Bind(bind)</c> in one call.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TNewError> Bind<TNewError>(Func<Result<TNewError>> bind, Func<TError, TNewError> mapError)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess ? bind() : Result<TNewError>.Failure(mapError(result.Error));
		}

		/// <summary>
		/// Binds the successful outcome to another operation that produces a value and can itself succeed or
		/// fail, widening the existing error to the bound operation's error type when this result represents
		/// failure.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The function to invoke when this result represents success.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <remarks>
		/// The value-producing counterpart of the widening unit <c>Bind</c>. It is equivalent to
		/// <c>MapError(mapError).Bind(bind)</c> in one call.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Result<TResult, TNewError> Bind<TResult, TNewError>(
			Func<Result<TResult, TNewError>> bind,
			Func<TError, TNewError> mapError
		)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess ? bind() : Result<TResult, TNewError>.Failure(mapError(result.Error));
		}

		/// <summary>
		/// Binds the successful outcome to an asynchronous unit operation that can itself succeed or fail,
		/// widening the existing error to the bound operation's error type when this result represents failure.
		/// </summary>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The asynchronous function to invoke when this result represents success.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TNewError>> BindAsync<TNewError>(
			Func<Task<Result<TNewError>>> bind,
			Func<TError, TNewError> mapError
		)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess ? bind() : Task.FromResult(Result<TNewError>.Failure(mapError(result.Error)));
		}

		/// <summary>
		/// Binds the successful outcome to an asynchronous operation that produces a value and can itself succeed
		/// or fail, widening the existing error to the bound operation's error type when this result represents
		/// failure.
		/// </summary>
		/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
		/// <typeparam name="TNewError">The error type of the bound operation and of the returned failure.</typeparam>
		/// <param name="bind">The asynchronous function to invoke when this result represents success.</param>
		/// <param name="mapError">
		/// The function that widens the existing error into <typeparamref name="TNewError"/>. Only invoked for a
		/// failure, so a successful result maps nothing.
		/// </param>
		/// <returns>
		/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a
		/// failed result whose error is <paramref name="mapError"/> applied to the existing error.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="bind"/> or <paramref name="mapError"/> is null.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Task<Result<TResult, TNewError>> BindAsync<TResult, TNewError>(
			Func<Task<Result<TResult, TNewError>>> bind,
			Func<TError, TNewError> mapError
		)
		{
			ArgumentNullException.ThrowIfNull(bind);
			ArgumentNullException.ThrowIfNull(mapError);
			RequireInitialized(result);

			return result.IsSuccess
				? bind()
				: Task.FromResult(Result<TResult, TNewError>.Failure(mapError(result.Error)));
		}

		#endregion

		#region Throwing

		/// <summary>
		/// Returns the <see cref="Success"/> marker, or throws a <see cref="ResultException{TError}"/> carrying
		/// the error when this result represents failure.
		/// </summary>
		/// <returns>The <see cref="Success"/> marker.</returns>
		/// <remarks>
		/// This is the deliberate result-to-exception escape hatch: reach for it at a boundary that must throw, or
		/// in a test, rather than to handle an expected failure, which is normally a value. An uninitialized result
		/// is a bug, so it is reported with <see cref="InvalidOperationException"/> rather than
		/// <see cref="ResultException{TError}"/>.
		/// </remarks>
		/// <exception cref="ResultException{TError}">
		/// The result represents failure; the thrown exception exposes the error through
		/// <see cref="ResultException{TError}.Error"/>.
		/// </exception>
		/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
		public Success Throw()
		{
			RequireInitialized(result);

			return result.IsSuccess ? Success.Instance : throw new ResultException<TError>(result.Error);
		}

		#endregion
	}

	/// <summary>
	/// Throws when <paramref name="result"/> was never initialized.
	/// </summary>
	/// <remarks>
	/// The members on <see cref="Result{TValue, TError}"/> throw the same exception for an uninitialized
	/// result, and this mirrors it (including the message) for the extension operations.
	/// </remarks>
	static void RequireInitialized<TValue, TError>(Result<TValue, TError> result)
	{
		if (!result.IsInitialized)
			throw new InvalidOperationException("The result is uninitialized.");
	}

	/// <summary>
	/// Throws when <paramref name="result"/> was never initialized.
	/// </summary>
	/// <remarks>
	/// The unit-result counterpart of the overload above, with the same message and the same contract.
	/// </remarks>
	static void RequireInitialized<TError>(Result<TError> result)
	{
		if (!result.IsInitialized)
			throw new InvalidOperationException("The result is uninitialized.");
	}
}
