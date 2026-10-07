namespace Purview.Results.Examples.ValueObjects.Zod;

/// <summary>
/// A tenant.
/// </summary>
/// <param name="Id">The identifier of the tenant.</param>
/// <param name="Name">The display name of the tenant.</param>
/// <param name="Enabled">Whether the tenant may be used.</param>
readonly record struct Tenant(TenantId Id, string Name, bool Enabled);
