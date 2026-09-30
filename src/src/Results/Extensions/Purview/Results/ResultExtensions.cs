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
}
