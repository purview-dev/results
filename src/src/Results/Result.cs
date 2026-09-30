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
}
