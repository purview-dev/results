namespace Purview.Results;

/// <summary>
/// Receives a result's successful value with its static type intact.
/// </summary>
/// <typeparam name="TState">State passed through to the visit, so the visitor can stay allocation-free.</typeparam>
/// <typeparam name="TReturn">What the visit produces.</typeparam>
/// <remarks>
/// <para>
/// <see cref="IResultValue.SuccessValue"/> is typed <see cref="object"/>, which is the right shape for
/// inspection but loses the value's type. Anything that has to *act* on the value generically — serializing
/// it, for example — would then bind its generic parameter to <see cref="object"/>, which under trimming or
/// Native AOT leaves the serializer with no resolvable type information, and in ASP.NET Core also destroys
/// the response-type metadata OpenAPI infers.
/// </para>
/// <para>
/// This is the double-dispatch escape: the result type knows its own <c>TValue</c> statically, so
/// <see cref="IResultValue.AcceptSuccess{TState, TReturn}"/> can hand it to
/// <see cref="VisitSuccess{TValue}"/> with the real type. No reflection, no boxing of the generic parameter.
/// </para>
/// </remarks>
public interface IResultValueVisitor<in TState, out TReturn>
{
	/// <summary>
	/// Called with the successful value, typed.
	/// </summary>
	/// <typeparam name="TValue">The static type of the successful value.</typeparam>
	/// <param name="value">The successful value.</param>
	/// <param name="state">The state supplied to <see cref="IResultValue.AcceptSuccess{TState, TReturn}"/>.</param>
	/// <returns>The result of the visit.</returns>
	/// <remarks>
	/// For a unit <see cref="Result{TError}"/> the value is the <see cref="Success"/> marker, so a visitor
	/// that distinguishes "succeeded with a payload" from "succeeded with nothing" tests for that type.
	/// </remarks>
	TReturn VisitSuccess<TValue>(TValue value, TState state);
}
