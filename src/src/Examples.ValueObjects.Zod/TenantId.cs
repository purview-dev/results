using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects;
using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.Results.Examples.ValueObjects.Zod;

/// <summary>
/// A custom rule that validates a scalar value object <em>as a unit</em>, reading it through its
/// <see cref="IScalarValueObject{TSelf, TValue}"/> contract.
/// </summary>
/// <typeparam name="TSelf">The scalar value object the rule validates.</typeparam>
/// <param name="Code">An optional code that overrides <see cref="ErrorCode"/> for one annotated scalar.</param>
/// <param name="Message">An optional message that overrides <see cref="MessageFormat"/>.</param>
/// <remarks>
/// Implementing <see cref="IZodRule"/> lets the rule own the reported error code and origin, which is what makes
/// one attribute able to report a domain code (<c>invalid_tenant_id</c>) and mark the failure as coming from the
/// value object (<c>value_object</c>) at the same time.
/// </remarks>
readonly record struct NonEmptyTenantIdRule<TSelf>(string? Code = null, string? Message = null)
	: IValidationRule<TSelf>,
		IZodRule
	where TSelf : IScalarValueObject<TSelf, Guid>
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_value";

	/// <summary>Gets the message format used when the rule reports its own message.</summary>
	public const string MessageFormat = "Value must not be empty.";

	/// <summary>Validates that the scalar does not hold the sentinel <see cref="Guid.Empty"/>.</summary>
	/// <param name="value">The value to validate.</param>
	/// <returns><see langword="true"/> when the value is not the empty identifier.</returns>
	public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

	/// <summary>Gets the error message for a failed validation.</summary>
	/// <param name="value">The value that failed validation.</param>
	/// <returns>The configured message, or the rule's default.</returns>
	public string GetErrorMessage(in TSelf value) => Message ?? MessageFormat;

	// IValidationRule<T>.Code is the rule's own default; IZodRule.Code is the optional per-use override, so the
	// generator prefers a code supplied where the attribute is applied and falls back to ErrorCode.
	string IValidationRule<TSelf>.Code => Code ?? ErrorCode;

	/// <inheritdoc />
	string? IZodRule.Code => Code;

	/// <inheritdoc />
	string? IZodRule.Origin => "value_object";
}

/// <summary>
/// Maps <see cref="NonEmptyTenantIdRule{TSelf}"/> to a type-level attribute. The ZodSharp generator closes the
/// open generic with the annotated scalar, so the rule validates the value object as a unit and reports an empty
/// path.
/// </summary>
[ZodRule(typeof(NonEmptyTenantIdRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
sealed class NonEmptyTenantIdAttribute : ValidationAttribute
{
	/// <summary>Gets or sets the error code reported for this application of the rule.</summary>
	public string? Code { get; set; }

	/// <summary>Gets or sets the error message reported for this application of the rule.</summary>
	public string? Message { get; set; }
}

/// <summary>
/// The identifier of a tenant, as a scalar value object.
/// </summary>
/// <remarks>
/// <c>[Scalar]</c> makes the value-object generator emit <c>Create</c>/<c>Hydrate</c>, and <c>[ZodSchema]</c>
/// makes ZodSharp's generator emit <c>TenantIdSchema</c> from the type-level rule below, so the value object's
/// own invariant is expressed as schema rules rather than hand-written guards. <c>Hydrate</c> never runs them,
/// which is what keeps rehydrating persisted values replay-safe.
/// </remarks>
[Scalar]
[ZodSchema]
[NonEmptyTenantId(Code = "invalid_tenant_id", Message = "A tenant id must not be empty.")]
readonly partial record struct TenantId
{
	/// <summary>Gets the underlying identifier value.</summary>
	public Guid Value { get; }
}
