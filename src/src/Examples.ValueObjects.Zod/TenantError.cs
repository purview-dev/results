using System.Collections.Immutable;
using Purview.Results.ZodSharp;
using ZodSharp.Core;

namespace Purview.Results.Examples.ValueObjects.Zod;

/// <summary>
/// The expected failures of the tenant registration operation.
/// </summary>
/// <remarks>
/// The failure that carries ZodSharp validation errors is a union case like any other, so a rejected value object
/// flows through the same result pipeline as a duplicate tenant.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantInputInvalid, TenantAlreadyExists);

/// <summary>
/// The supplied tenant id did not satisfy the value object's type-level rule.
/// </summary>
/// <param name="TenantId">The rejected identifier.</param>
/// <param name="Errors">The validation errors the tenant id's schema reported.</param>
/// <remarks>
/// Implementing <see cref="IValidationErrorCarrier"/> lets an HTTP layer turn the failure into a validation
/// problem without knowing the error type — including the rule's code and origin.
/// </remarks>
readonly record struct TenantInputInvalid(Guid TenantId, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantAlreadyExists(TenantId TenantId);