using Purview.Results.SourceGeneration;

namespace Purview.Results.Examples.AspNetCore;

// The union declaration does not synthesise value equality, so CA1815 is not meaningful for it.
#pragma warning disable CA1815
/// <summary>
/// The expected failures of a tenant operation.
/// </summary>
/// <remarks>
/// The mapping registered for a case type handles that case, and the mapping registered for the union handles
/// every case without a mapping of its own.
/// </remarks>
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
#pragma warning restore CA1815

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
