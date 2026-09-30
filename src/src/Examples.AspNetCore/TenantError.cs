using Purview.Results.SourceGeneration;

namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// The expected failures of a tenant operation.
/// </summary>
/// <remarks>
/// The mapping registered for a case type handles that case, and the mapping registered for the union handles
/// every case without a mapping of its own.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

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
