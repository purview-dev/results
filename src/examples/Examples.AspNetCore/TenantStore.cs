namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// An in-memory tenant store, standing in for a database.
/// </summary>
/// <remarks>
/// Every operation returns <c>Result&lt;Tenant, TenantError&gt;</c>: an expected failure such as a missing
/// tenant is a value, and only exceptional circumstances would throw.
/// </remarks>
public sealed class InMemoryTenantStore
{
	readonly Dictionary<TenantId, Tenant> _tenants = new()
	{
		[new TenantId("acme")] = new Tenant(new TenantId("acme"), "Acme", Enabled: true),
		[new TenantId("globex")] = new Tenant(new TenantId("globex"), "Globex", Enabled: false),
	};

	/// <summary>
	/// Gets the tenant with the supplied identifier.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>The tenant, or a <see cref="TenantNotFound"/> failure.</returns>
	public Result<Tenant, TenantError> GetTenant(TenantId tenantId) =>
		_tenants.TryGetValue(tenantId, out var tenant)
			? Result<Tenant, TenantError>.Success(tenant)
			: new TenantNotFound(tenantId).AsFailure<Tenant>();

	/// <summary>
	/// Gets the tenant with the supplied identifier, rejecting one that is disabled.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>
	/// The tenant, a <see cref="TenantNotFound"/> failure, or a <see cref="TenantDisabled"/> failure.
	/// </returns>
	public Result<Tenant, TenantError> GetEnabledTenant(TenantId tenantId) =>
		GetTenant(tenantId).Ensure(tenant => tenant.Enabled, tenant => new TenantDisabled(tenant.Id));

	/// <summary>
	/// Creates a tenant.
	/// </summary>
	/// <param name="tenantId">The identifier of the new tenant.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The created tenant, or a <see cref="TenantAlreadyExists"/> failure.</returns>
	public Result<Tenant, TenantError> CreateTenant(TenantId tenantId, string name)
	{
		if (_tenants.ContainsKey(tenantId))
			return new TenantAlreadyExists(tenantId).AsFailure<Tenant>();

		Tenant tenant = new(tenantId, name, Enabled: true);
		_tenants[tenantId] = tenant;

		return Result<Tenant, TenantError>.Success(tenant);
	}
}
