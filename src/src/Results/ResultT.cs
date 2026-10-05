using System.Diagnostics.CodeAnalysis;

namespace Purview.Results;

/// <summary>
/// Represents the result of an operation that either succeeds with a value
/// or fails with an error.
/// </summary>
/// <typeparam name="TValue">The type of value produced when the operation succeeds.</typeparam>
/// <typeparam name="TError">The type of error produced when the operation fails.</typeparam>
public readonly record struct Result<TValue, TError> : IResultValue
{
	readonly ResultState _state = ResultState.Uninitialized;

	Result(TValue value)
	{
		Value = value;
		Error = default!;
		_state = ResultState.Success;
	}

	Result(TError error)
	{
		Value = default!;
		Error = error;
		_state = ResultState.Failure;
	}

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	[MemberNotNullWhen(true, nameof(Value))]
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

	object? IResultValue.SuccessValue => IsSuccess ? Value : null;

	object? IResultValue.ErrorValue => IsFailure ? Error : null;

	/// <summary>
	/// Gets the successful value.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The result represents a failure.
	/// </exception>
	[AllowNull]
	public TValue Value =>
		IsSuccess
			? field
			: throw new InvalidOperationException("The value of a non-successful result cannot be accessed.");

	/// <summary>
	/// Gets the error.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The result represents a success.
	/// </exception>
	[AllowNull]
	public TError Error =>
		IsFailure ? field : throw new InvalidOperationException("The error of a non-failed result cannot be accessed.");

	/// <inheritdoc />
	public override string ToString() =>
		_state switch
		{
			ResultState.Success => $"Success({Value})",
			ResultState.Failure => $"Failure({Error})",
			ResultState.Uninitialized or _ => "Uninitialized",
		};

	/// <summary>
	/// Matches the result and invokes the appropriate function for its current state.
	/// </summary>
	/// <typeparam name="TResult">The type of value returned by both match functions.</typeparam>
	/// <param name="success">
	/// The function to invoke with the successful value when the result represents success.
	/// </param>
	/// <param name="failure">
	/// The function to invoke with the error when the result represents failure.
	/// </param>
	/// <returns>The value returned by the selected match function.</returns>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public TResult Match<TResult>(Func<TValue, TResult> success, Func<TError, TResult> failure)
	{
		ArgumentNullException.ThrowIfNull(success);
		ArgumentNullException.ThrowIfNull(failure);

		return _state switch
		{
			ResultState.Success => success(Value),
			ResultState.Failure => failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Maps the successful value to a new value while preserving the error type.
	/// </summary>
	/// <typeparam name="TResult">The type of the mapped successful value.</typeparam>
	/// <param name="map">The function used to transform the successful value.</param>
	/// <returns>
	/// A successful result containing the mapped value when this result represents success;
	/// otherwise, a failed result containing the existing error.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return _state switch
		{
			ResultState.Success => Result<TResult, TError>.Success(map(Value)),
			ResultState.Failure => Result<TResult, TError>.Failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Binds the successful value to another operation that can itself succeed or fail,
	/// while preserving the error type.
	/// </summary>
	/// <typeparam name="TResult">The successful value type of the bound operation.</typeparam>
	/// <param name="bind">
	/// The function to invoke with the successful value.
	/// </param>
	/// <returns>
	/// The result returned by <paramref name="bind"/> when this result represents success;
	/// otherwise, a failed result containing the existing error.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> bind)
	{
		ArgumentNullException.ThrowIfNull(bind);

		return _state switch
		{
			ResultState.Success => bind(Value),
			ResultState.Failure => Result<TResult, TError>.Failure(Error),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	/// <summary>
	/// Maps the error to a new error type while preserving the successful value.
	/// </summary>
	/// <typeparam name="TResultError">The mapped error type.</typeparam>
	/// <param name="map">The function used to transform the error.</param>
	/// <returns>
	/// A failed result containing the mapped error when this result represents failure;
	/// otherwise, a successful result containing the existing value.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The result is uninitialized.
	/// </exception>
	public Result<TValue, TResultError> MapError<TResultError>(Func<TError, TResultError> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return _state switch
		{
			ResultState.Success => Result<TValue, TResultError>.Success(Value),
			ResultState.Failure => Result<TValue, TResultError>.Failure(map(Error)),
			ResultState.Uninitialized or _ => throw CreateUninitializedException(),
		};
	}

	static InvalidOperationException CreateUninitializedException() => new("The result is uninitialized.");

	public static implicit operator Result<TValue, TError>(TValue value) => Success(value);

	public static implicit operator Result<TValue, TError>(TError error) => Failure(error);

	/// <summary>
	/// Creates a successful result.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static Result<TValue, TError> Success(TValue value) => new(value);

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static Result<TValue, TError> Failure(TError error) => new(error);

	enum ResultState : byte
	{
		Uninitialized,

		Success,

		Failure,
	}
}
