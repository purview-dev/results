using Purview.Results.ZodSharp;
using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.Examples.AspNetCore.Zod;

/// <summary>
/// The expected failures of the tenant registration operation.
/// </summary>
/// <remarks>
/// Only the case that carries validation errors implements <see cref="IValidationErrorCarrier"/>, so only that
/// case is rendered as a validation problem by the fallback; a case with its own mapping still wins.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantAlreadyExists, TenantInputInvalid);

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantAlreadyExists(TenantId TenantId);

/// <summary>
/// The supplied input did not satisfy its schema.
/// </summary>
/// <param name="Input">The rejected input.</param>
/// <param name="Errors">The validation errors reported by <c>TenantInputSchema</c>.</param>
readonly record struct TenantInputInvalid(TenantInput Input, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}
