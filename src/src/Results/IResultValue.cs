namespace Purview.Results;

/// <summary>
/// Provides a non-generic, read-only view of a result.
/// </summary>
/// <remarks>
/// Infrastructure that is not generic over the value and error types — an ASP.NET Core endpoint filter, for
/// example — can inspect a result through this interface instead of using reflection. Accessing
/// <see cref="SuccessValue"/> or <see cref="ErrorValue"/> never throws: the accessor that does not describe the
/// current state returns <see langword="null"/>.
/// <para>
/// A successful value result reports its value; a successful unit <see cref="Result{TError}"/> reports the
/// <see cref="Success"/> marker, which is how infrastructure can tell a unit success from one that carries a
/// payload.
/// </para>
/// </remarks>
public interface IResultValue
{
	/// <summary>
	/// Gets a value indicating whether the result has been initialized.
	/// </summary>
	bool IsInitialized { get; }

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	bool IsSuccess { get; }

	/// <summary>
	/// Gets the successful value, or <see langword="null"/> when the result is not a success.
	/// </summary>
	object? SuccessValue { get; }

	/// <summary>
	/// Gets the error, or <see langword="null"/> when the result is not a failure.
	/// </summary>
	object? ErrorValue { get; }
}
