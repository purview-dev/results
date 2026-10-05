using System.Diagnostics.CodeAnalysis;

namespace Purview.Results;

/// <summary>
/// The exception thrown by <c>Throw()</c> when a result represents a failure.
/// </summary>
/// <remarks>
/// <para>
/// This exception is a deliberate escape hatch: it turns an expected failure — which this library otherwise
/// treats as a value — into an exception at a boundary that must throw. The error the result carried is
/// available through <see cref="Error"/>.
/// </para>
/// <para>
/// An uninitialized result (<c>default</c>) is a bug rather than an expected failure, so <c>Throw()</c> reports
/// it with <see cref="InvalidOperationException"/> and never constructs this exception for it.
/// </para>
/// </remarks>
public class ResultException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResultException"/> class.
	/// </summary>
	public ResultException() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="ResultException"/> class with the supplied message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public ResultException(string? message)
		: base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="ResultException"/> class with the supplied message and inner
	/// exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public ResultException(string? message, Exception? innerException)
		: base(message, innerException) { }

	/// <summary>
	/// Gets the error the failed result carried, or <see langword="null"/> when this exception was not created
	/// from a result error.
	/// </summary>
	public object? Error { get; protected set; }

	/// <summary>
	/// Creates the message for an error carried by a failed result.
	/// </summary>
	/// <param name="error">The error the result carried.</param>
	/// <returns>The message that describes the error.</returns>
	protected static string CreateMessage(object? error) =>
		error is null ? "The result represents a failure." : $"The result represents a failure: {error}";
}

/// <summary>
/// The exception thrown by <c>Throw()</c> for a <see cref="Result{TValue, TError}"/> or a unit
/// <see cref="Result{TError}"/>, exposing the error with its original type.
/// </summary>
/// <typeparam name="TError">The type of error the result carried.</typeparam>
[SuppressMessage(
	"Design",
	"CA1032:Implement standard exception constructors",
	Justification = "The error-carrying constructor is this type's contract; the standard constructors are provided by the non-generic ResultException base."
)]
public sealed class ResultException<TError> : ResultException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResultException{TError}"/> class for the supplied error.
	/// </summary>
	/// <param name="error">The error the failed result carried.</param>
	public ResultException(TError error)
		: base(CreateMessage(error))
	{
		base.Error = error;
	}

	/// <summary>
	/// Gets the error the failed result carried, with its original type.
	/// </summary>
	public new TError Error => (TError)base.Error!;
}
