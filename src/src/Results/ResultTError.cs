using System.Diagnostics.CodeAnalysis;

namespace Purview.Results;

/// <summary>
/// Represents the result of an operation that either succeeds with no value or fails with an error.
/// </summary>
/// <typeparam name="TError">The type of error produced when the operation fails.</typeparam>
/// <remarks>
/// <para>
/// This is the value-less counterpart of <see cref="Result{TValue, TError}"/>: it carries the same three states
/// — <c>Uninitialized</c> (the <c>default</c> value), <c>Success</c> and <c>Failure</c> — and the same
/// throw-on-misuse contract, but its success carries no payload. An operation that has nothing to return on
/// success and everything to explain on failure, such as a command or a validation-only step, can therefore be
/// expressed as a result instead of a <c>bool</c> or an exception.
/// </para>
/// <para>
/// A success holds the <see cref="Success"/> marker, so <c>Result&lt;TError&gt;</c> composes with the rest of the
/// suite: <c>IResultValue.SuccessValue</c> returns the marker, and the ASP.NET Core mapper answers a successful
/// unit result with <c>204 No Content</c>.
/// </para>
/// </remarks>
public readonly record struct Result<TError> : IResultValue
{
	readonly ResultState _state = ResultState.Uninitialized;

	Result(Success _)
	{
		_state = ResultState.Success;
	}

	Result(TError error)
	{
		Error = error;
		_state = ResultState.Failure;
	}

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	public bool IsSuccess => _state == ResultState.Success;

	/// <summary>
	/// Gets a value indicating whether the operation failed.
	/// </summary>
	[MemberNotNullWhen(true, nameof(Error))]
	public bool IsFailure => _state == ResultState.Failure;

	/// <summary>
	/// Gets a value indicating whether the result has been initialized.
	/// </summary>
	public bool IsInitialized => _state != ResultState.Uninitialized;

	bool IResultValue.IsInitialized => IsInitialized;

	bool IResultValue.IsSuccess => IsSuccess;

	object? IResultValue.SuccessValue => IsSuccess ? global::Purview.Results.Success.Instance : null;

	object? IResultValue.ErrorValue => IsFailure ? Error : null;

	/// <summary>
	/// Gets the error.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The result represents a success.
	/// </exception>
	[AllowNull]
	public TError Error
	{
		get =>
			IsFailure
				? field
				: throw new InvalidOperationException("The error of a non-failed result cannot be accessed.");
	} = default!;

	/// <inheritdoc />
	public override string ToString() =>
		_state switch
		{
			ResultState.Success => "Success",
			ResultState.Failure => $"Failure({Error})",
			ResultState.Uninitialized or _ => "Uninitialized",
		};

	/// <summary>
	/// Matches the result and invokes the appropriate function for its current state.
	/// </summary>
	/// <typeparam name="TResult">The type of value returned by both match functions.</typeparam>
	/// <param name="success">The function to invoke when the result represents success.</param>
	/// <param name="failure">
	/// The function to invoke with the error when the result represents failure.
	/// </param>
	/// <returns>The value returned by the selected match function.</returns>
	/// <remarks>
	/// The success function takes no argument because a successful unit result carries no value; the
	/// <see cref="Success"/> marker is not passed to it.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="success"/> or <paramref name="failure"/> is null.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public TResult Match<TResult>(Func<TResult> success, Func<TError, TResult> failure)
	{
		ArgumentNullException.ThrowIfNull(success);
		ArgumentNullException.ThrowIfNull(failure);

		return _state switch
		{
			ResultState.Success => success(),
			ResultState.Failure => failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Maps the successful outcome to a value while preserving the error type.
	/// </summary>
	/// <typeparam name="TResult">The type of the mapped successful value.</typeparam>
	/// <param name="map">The function used to produce the successful value.</param>
	/// <returns>
	/// A successful result containing the produced value when this result represents success; otherwise, a failed
	/// result containing the existing error.
	/// </returns>
	/// <remarks>
	/// The mapping function takes no argument because a successful unit result carries no value.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public Result<TResult, TError> Map<TResult>(Func<TResult> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return _state switch
		{
			ResultState.Success => Result<TResult, TError>.Success(map()),
			ResultState.Failure => Result<TResult, TError>.Failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Binds the successful outcome to another unit operation that can itself succeed or fail, while preserving
	/// the error type.
	/// </summary>
	/// <param name="bind">The function to invoke when this result represents success.</param>
	/// <returns>
	/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a failed
	/// result containing the existing error.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="bind"/> is null.</exception>
	/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
	public Result<TError> Bind(Func<Result<TError>> bind)
	{
		ArgumentNullException.ThrowIfNull(bind);

		return _state switch
		{
			ResultState.Success => bind(),
			ResultState.Failure => Result<TError>.Failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Binds the successful outcome to an operation that produces a value and can itself succeed or fail, while
	/// preserving the error type.
	/// </summary>
	/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
	/// <param name="bind">The function to invoke when this result represents success.</param>
	/// <returns>
	/// The result returned by <paramref name="bind"/> when this result represents success; otherwise, a failed
	/// result containing the existing error.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="bind"/> is null.</exception>
	/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
	public Result<TResult, TError> Bind<TResult>(Func<Result<TResult, TError>> bind)
	{
		ArgumentNullException.ThrowIfNull(bind);

		return _state switch
		{
			ResultState.Success => bind(),
			ResultState.Failure => Result<TResult, TError>.Failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Maps the error to a new error type while preserving the successful outcome.
	/// </summary>
	/// <typeparam name="TResultError">The mapped error type.</typeparam>
	/// <param name="map">The function used to transform the error.</param>
	/// <returns>
	/// A failed result containing the mapped error when this result represents failure; otherwise, a successful
	/// unit result.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	/// <exception cref="InvalidOperationException">The result is uninitialized.</exception>
	public Result<TResultError> MapError<TResultError>(Func<TError, TResultError> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return _state switch
		{
			ResultState.Success => Result<TResultError>.Success(),
			ResultState.Failure => Result<TResultError>.Failure(map(Error)),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	static InvalidOperationException CreateUninitializedException() => new("The result is uninitialized.");

	public static implicit operator Result<TError>(Success _) => Success();

	public static implicit operator Result<TError>(TError error) => Failure(error);

	/// <summary>
	/// Creates a successful unit result.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static Result<TError> Success() => new(Results.Success.Instance);

	/// <summary>
	/// Creates a failed unit result.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static Result<TError> Failure(TError error) => new(error);

	enum ResultState : byte
	{
		Uninitialized,

		Success,

		Failure,
	}
}
