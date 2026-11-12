using System.Collections.Immutable;
using Purview.Results.ZodSharp;
using ZodSharp.Core;

namespace Purview.Results.Examples.Blazor.Server;

/// <summary>
/// The expected failures of a tenant operation.
/// </summary>
/// <remarks>
/// The union is the error type of <c>Result&lt;Tenant, TenantError&gt;</c>. <c>TenantInputInvalid</c> carries the
/// ZodSharp errors, and implements <see cref="IValidationErrorCarrier"/> so a host can render them as a
/// validation problem without knowing this sample's error type.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantInputInvalid, TenantNotFound, TenantAlreadyExists);

/// <summary>
/// The submitted input failed its <c>[ZodSchema]</c> validation.
/// </summary>
/// <param name="Errors">The validation errors reported by the schema.</param>
readonly record struct TenantInputInvalid(ImmutableArray<ValidationError> Errors) : IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// The requested tenant does not exist.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantAlreadyExists(TenantId TenantId);
