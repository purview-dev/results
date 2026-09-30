using System.Collections.Immutable;

using Purview.Results.SourceGeneration;
using Purview.Results.ZodSharp;

using ZodSharp.Core;

namespace Purview.Results.Examples.Zod;

// The union declaration does not synthesise value equality, so CA1815 is not meaningful for it.
#pragma warning disable CA1815
/// <summary>
/// The expected failures of the tenant registration operation.
/// </summary>
/// <remarks>
/// The failure that carries ZodSharp validation errors is a union case like any other, so a rejected input
/// flows through the same result pipeline as a missing or disabled tenant.
/// </remarks>
[GenerateResult]
public readonly union TenantError(TenantInputInvalid, TenantNotFound, TenantDisabled, TenantAlreadyExists);
#pragma warning restore CA1815

/// <summary>
/// The supplied input did not satisfy its schema.
/// </summary>
/// <param name="Input">The rejected input.</param>
/// <param name="Errors">The validation errors reported by <c>TenantInputSchema</c>.</param>
/// <remarks>
/// Implementing <see cref="IValidationErrorCarrier"/> lets an HTTP layer turn the failure into a validation
/// problem without knowing the error type.
/// </remarks>
public readonly record struct TenantInputInvalid(TenantInput Input, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// The requested tenant does not exist.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>
/// The requested tenant exists but is disabled.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantDisabled(TenantId TenantId);

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantAlreadyExists(TenantId TenantId);
