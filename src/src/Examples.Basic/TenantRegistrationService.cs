namespace Purview.Results.Examples.Basic;

/// <summary>
/// Registers tenants, calling the billing service first and widening its error into the registration contract.
/// </summary>
/// <remarks>
/// The registration error is <see cref="RegisterTenantError"/>: a union whose cases are the tenant error union
/// and the billing error union. A billing failure therefore flows through the same result as a tenant failure,
/// and the registration call site never names a billing case.
/// </remarks>
sealed class TenantRegistrationService(InMemoryTenantStore store, BillingService billing)
{
	/// <summary>
	/// Registers a tenant, chaining the billing reservation and the store write with the widening <c>Bind</c>.
	/// </summary>
	/// <param name="tenantId">The identifier of the new tenant.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The registered tenant, or a <see cref="RegisterTenantError"/> describing why it failed.</returns>
	public Result<Tenant, RegisterTenantError> Register(TenantId tenantId, string name) =>
		// The widening Bind maps a billing failure into RegisterTenantError and leaves a success to the store
		// write, which maps its own TenantError into the same union. Both `error => error` lambdas rely on the
		// implicit union conversion into RegisterTenantError.
		billing.ReserveQuota(tenantId).Bind(quota => CreateTenant(quota, name), error => error);

	/// <summary>
	/// Registers a tenant using the guard idiom: bomb out on a billing failure before touching the store.
	/// </summary>
	/// <param name="tenantId">The identifier of the new tenant.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The registered tenant, or a <see cref="RegisterTenantError"/> describing why it failed.</returns>
	public Result<Tenant, RegisterTenantError> RegisterWithGuard(TenantId tenantId, string name)
	{
		var reserved = billing.ReserveQuota(tenantId);

		// BillingError is a case of RegisterTenantError, so its generated helper produces the composite
		// failure directly.
		if (reserved.TryGetError(out var billingError))
			return billingError.AsFailure<Tenant>();

		// The guard idiom: the method succeeds with a Tenant, so the failure is produced by the helper the generator
		return CreateTenant(reserved.Value, name);
	}

	Result<Tenant, RegisterTenantError> CreateTenant(Quota quota, string name) =>
		// MapError names the target union explicitly, because a bare `error => error` would infer TenantError.
		store.CreateTenant(quota.TenantId, name).MapError<RegisterTenantError>(error => error);
}
