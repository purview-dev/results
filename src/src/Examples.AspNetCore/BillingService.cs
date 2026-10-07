namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// An in-memory billing service, standing in for a downstream service boundary.
/// </summary>
sealed class BillingService
{
	/// <summary>
	/// Gets the billing account for the tenant.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>The account, or a <see cref="BillingError"/> describing why it could not be loaded.</returns>
	public Result<BillingAccount, BillingError> GetAccount(TenantId tenantId) =>
		tenantId.Value switch
		{
			"acme" => new BillingServiceUnavailable("the billing service is offline").AsFailure<BillingAccount>(),
			"hooli" => new BillingAccountMissing(tenantId).AsFailure<BillingAccount>(),
			_ => Result<BillingAccount, BillingError>.Success(new BillingAccount(tenantId, $"acct-{tenantId.Value}")),
		};
}
