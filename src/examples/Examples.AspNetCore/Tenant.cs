namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// The identifier of a tenant.
/// </summary>
/// <param name="Value">The identifier value.</param>
public readonly record struct TenantId(string Value);

/// <summary>
/// A tenant.
/// </summary>
/// <param name="Id">The identifier of the tenant.</param>
/// <param name="Name">The display name of the tenant.</param>
/// <param name="Enabled">Whether the tenant may be used.</param>
public readonly record struct Tenant(TenantId Id, string Name, bool Enabled);
