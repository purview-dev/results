namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// Looks up a tenant's billing account, widening the tenant error union into the billing operation's contract.
/// </summary>
sealed class TenantBillingService(InMemoryTenantStore store, BillingService billing)
{
	/// <summary>
	/// Gets the billing account for the tenant, failing if the tenant does not exist or is disabled.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>The account, or a <see cref="TenantOperationError"/> describing why it could not be loaded.</returns>
	public Result<BillingAccount, TenantOperationError> GetAccount(TenantId tenantId)
	{
		var tenant = store.GetEnabledTenant(tenantId);

		// TenantError is a case of TenantOperationError, so its generated helper produces the composite failure
		// directly, and the mapping registered for TenantError covers it in the HTTP layer.
		if (tenant.TryGetError(out var tenantError))
			return tenantError.AsFailure<BillingAccount>();

		// MapError names the target union explicitly, because a bare `error => error` would infer BillingError.
		return billing.GetAccount(tenant.Value.Id).MapError<TenantOperationError>(error => error);
	}
}
