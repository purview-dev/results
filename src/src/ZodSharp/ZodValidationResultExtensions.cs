using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp;

/// <summary>
/// Bridges <see cref="ValidationResult{T}"/> into results so validation outcomes flow through the same result
/// pipeline as every other expected outcome instead of throwing.
/// </summary>
/// <remarks>
/// <para>
/// ZodSharp validation never throws: <c>Validate</c> returns a <see cref="ValidationResult{T}"/> carrying the
/// validated value on success and every <see cref="ValidationError"/> on failure. The <c>ToResult</c> adapter
/// translates that into a result whose error type the caller chooses, so a failure becomes an ordinary
/// error value (typically a union case) and a success carries the validated value unchanged.
/// </para>
/// <para>
/// Throwing remains reserved for exceptional circumstances — a lost database connection, for example; expected
/// validation states are mapped, not thrown.
/// </para>
/// <para>
/// When the success type of the surrounding method differs from the validated type, the failure is produced by the
/// generated <c>AsFailure&lt;TValue&gt;()</c> helper instead, because the validated value is not the value the
/// method succeeds with:
/// </para>
/// <code>
/// var validated = ProviderConnectionIdSchema.Validate(providerConnectionId);
/// if (!validated.IsSuccess)
/// {
///     return new ProviderConnectionInvalid(providerConnectionId, validated.Errors)
///         .AsFailure&lt;RepositoryReconciliationResult&gt;();
/// }
///
/// return await ReconcileCoreAsync(validated.Value, repositories, cancellationToken);
/// </code>
/// <para>
/// A factory returning a result rather than an error is deliberately not offered: for a lambda returning
/// <c>Result&lt;TValue, TError&gt;</c> the compiler prefers a <c>Func&lt;..., TError&gt;</c> parameter and would
/// silently nest the results. Naming the error type explicitly keeps the intent unambiguous.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// return RepositoryReconciliationResultSchema
///     .Validate(reconciliationResult)
///     .ToResult&lt;RepositoryReconciliationResult, ReconciliationError&gt;(errors =&gt;
///         new ReconciliationResultInvalid(reconciliationResult, errors)
///     );
/// </code>
/// </example>
public static class ZodValidationResultExtensions
{
	/// <summary>
	/// Converts a validation result into a result whose failure carries the error produced by
	/// <paramref name="onFailure"/>.
	/// </summary>
	/// <typeparam name="TValue">The type of the validated value.</typeparam>
	/// <typeparam name="TError">
	/// The error type of the result. A union type must be named explicitly, as in
	/// <c>ToResult&lt;RepositoryReconciliationResult, ReconciliationError&gt;(...)</c>, because a union case does
	/// not carry the union type that contains it.
	/// </typeparam>
	/// <param name="validation">The validation result to convert.</param>
	/// <param name="onFailure">
	/// Creates the error from the validation errors. Only invoked when validation failed, so a successful
	/// validation allocates no error.
	/// </param>
	/// <returns>
	/// A successful result carrying the validated value, or a failed result carrying the created error.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="onFailure"/> is null.</exception>
	public static Result<TValue, TError> ToResult<TValue, TError>(
		this ValidationResult<TValue> validation,
		Func<ImmutableArray<ValidationError>, TError> onFailure
	)
	{
		ArgumentNullException.ThrowIfNull(onFailure);

		return validation.IsSuccess
			? Result<TValue, TError>.Success(validation.Value)
			: Result<TValue, TError>.Failure(onFailure(validation.Errors));
	}

	/// <summary>
	/// Converts a validation result into a unit result whose failure carries the error produced by
	/// <paramref name="onFailure"/>, discarding the validated value.
	/// </summary>
	/// <typeparam name="TValue">The type of the validated value.</typeparam>
	/// <typeparam name="TError">
	/// The error type of the result. A union type must be named explicitly, as in
	/// <c>ToUnitResult&lt;TenantInput, TenantError&gt;(...)</c>, because a union case does not carry the union
	/// type that contains it.
	/// </typeparam>
	/// <param name="validation">The validation result to convert.</param>
	/// <param name="onFailure">
	/// Creates the error from the validation errors. Only invoked when validation failed, so a successful
	/// validation allocates no error.
	/// </param>
	/// <returns>
	/// A successful unit result, or a failed unit result carrying the created error.
	/// </returns>
	/// <remarks>
	/// Reach for this when the surrounding method validates a value but succeeds with nothing — a command that
	/// accepts an input and reports only why it was rejected. The validated value is deliberately dropped, which
	/// is why the value type still has to be named; use <see cref="ToResult{TValue, TError}"/> when the success
	/// has to carry it.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="onFailure"/> is null.</exception>
	public static Result<TError> ToUnitResult<TValue, TError>(
		this ValidationResult<TValue> validation,
		Func<ImmutableArray<ValidationError>, TError> onFailure
	)
	{
		ArgumentNullException.ThrowIfNull(onFailure);

		return validation.IsSuccess ? Result<TError>.Success() : Result<TError>.Failure(onFailure(validation.Errors));
	}
}
