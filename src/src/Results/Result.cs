namespace Purview.Results;

/// <summary>
/// Provides factory methods for creating operation results.
/// </summary>
public static class Result
{
	/// <summary>
	/// Creates a successful result containing the specified value.
	/// </summary>
	public static Result<TValue, TError> Success<TValue, TError>(TValue value) => Result<TValue, TError>.Success(value);

	/// <summary>
	/// Creates a failed result containing the specified error.
	/// </summary>
	public static Result<TValue, TError> Failure<TValue, TError>(TError error) => Result<TValue, TError>.Failure(error);

	/// <summary>
	/// Creates a successful unit result.
	/// </summary>
	/// <typeparam name="TError">The error type of the created result.</typeparam>
	/// <returns>A successful <see cref="Result{TError}"/>.</returns>
	public static Result<TError> Success<TError>() => Result<TError>.Success();

	/// <summary>
	/// Creates a failed unit result containing the specified error.
	/// </summary>
	/// <typeparam name="TError">The error type of the created result.</typeparam>
	/// <param name="error">The error the result carries.</param>
	/// <returns>A failed <see cref="Result{TError}"/>.</returns>
	public static Result<TError> Failure<TError>(TError error) => Result<TError>.Failure(error);
}
