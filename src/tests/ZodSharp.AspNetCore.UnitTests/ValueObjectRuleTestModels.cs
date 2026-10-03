using System.ComponentModel.DataAnnotations;
using Purview.ValueObjects;
using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

/// <summary>
/// A type-level ZodSharp rule that validates a scalar value object <em>as a unit</em>, reading it through its
/// <see cref="IScalarValueObject{TSelf, TValue}"/> contract and owning the reported code and origin.
/// </summary>
/// <remarks>
/// Implementing <see cref="IZodRule"/> is what lets one attribute report a domain code per annotated scalar, and
/// what makes <see cref="IZodRule.Origin"/> the axis the HTTP layer can key on.
/// </remarks>
/// <typeparam name="TSelf">The scalar value object the rule validates.</typeparam>
/// <param name="Code">An optional code that overrides <see cref="ErrorCode"/> for one annotated scalar.</param>
/// <param name="Message">An optional message that overrides <see cref="MessageFormat"/>.</param>
readonly record struct NonEmptyValidatedIdRule<TSelf>(string? Code = null, string? Message = null)
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

	string IValidationRule<TSelf>.Code => Code ?? ErrorCode;

	/// <inheritdoc />
	string? IZodRule.Code => Code;

	/// <inheritdoc />
	string? IZodRule.Origin => "value_object";
}

/// <summary>
/// Maps <see cref="NonEmptyValidatedIdRule{TSelf}"/> to a type-level attribute, so the ZodSharp generator closes
/// the open generic with the annotated scalar and the rule validates the value object as a unit.
/// </summary>
[ZodRule(typeof(NonEmptyValidatedIdRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
sealed class NonEmptyValidatedIdAttribute : ValidationAttribute
{
	/// <summary>Gets or sets the error code reported for this application of the rule.</summary>
	public string? Code { get; set; }

	/// <summary>Gets or sets the error message reported for this application of the rule.</summary>
	public string? Message { get; set; }
}

/// <summary>
/// A Guid-backed scalar value object whose validation is driven entirely by a type-level custom rule, so its
/// generated <c>Create</c> surfaces the rule's own code and origin while <c>Hydrate</c> stays replay-safe.
/// </summary>
[Scalar]
[ZodSchema]
[NonEmptyValidatedId(Code = "invalid_tenant_id", Message = "A tenant id must not be empty.")]
readonly partial record struct ValidatedTenantId
{
	/// <summary>Gets the underlying identifier value.</summary>
	public Guid Value { get; }
}
