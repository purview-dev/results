using Purview.Results.SourceGeneration;
using Purview.Results.ZodSharp;
using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.Examples.Zod;

/// <summary>
/// The expected failures of the tenant registration operation.
/// </summary>
/// <remarks>
/// The failure that carries ZodSharp validation errors is a union case like any other, so a rejected input
/// flows through the same result pipeline as a missing or disabled tenant.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantInputInvalid, TenantNotFound, TenantDisabled, TenantAlreadyExists);

/// <summary>
/// The supplied input did not satisfy its schema.
/// </summary>
/// <param name="Input">The rejected input.</param>
/// <param name="Errors">The validation errors reported by <c>TenantInputSchema</c>.</param>
/// <remarks>
/// Implementing <see cref="IValidationErrorCarrier"/> lets an HTTP layer turn the failure into a validation
/// problem without knowing the error type.
/// </remarks>
readonly record struct TenantInputInvalid(TenantInput Input, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// The requested tenant does not exist.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>
/// The requested tenant exists but is disabled.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantDisabled(TenantId TenantId);

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantAlreadyExists(TenantId TenantId);
