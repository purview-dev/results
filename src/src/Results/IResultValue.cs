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

	/// <summary>
	/// Hands the successful value to <paramref name="visitor"/> with its static type intact.
	/// </summary>
	/// <typeparam name="TState">State passed through to the visitor.</typeparam>
	/// <typeparam name="TReturn">What the visit produces.</typeparam>
	/// <param name="visitor">The visitor to invoke.</param>
	/// <param name="state">State to pass through, so the visitor need not capture.</param>
	/// <returns>Whatever the visitor returns.</returns>
	/// <remarks>
	/// Use this instead of <see cref="SuccessValue"/> whenever the value's type matters — serialization is
	/// the usual case. <see cref="SuccessValue"/> erases it to <see cref="object"/>, which under trimming or
	/// Native AOT leaves a serializer with nothing to resolve. See <see cref="IResultValueVisitor{TState, TReturn}"/>.
	/// <para>
	/// Like the other members here this never throws: an uninitialized or failed result still visits, passing
	/// the default value. Check <see cref="IsSuccess"/> first when that distinction matters.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="visitor"/> is null.</exception>
	TReturn AcceptSuccess<TState, TReturn>(IResultValueVisitor<TState, TReturn> visitor, TState state);
}
