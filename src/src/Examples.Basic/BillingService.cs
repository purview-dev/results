namespace Purview.Results.Examples.Basic;

/// <summary>
/// An in-memory billing service, standing in for a downstream service boundary.
/// </summary>
/// <remarks>
/// It fails with its own <see cref="BillingError"/> union, which the registration service widens into
/// <see cref="RegisterTenantError"/> instead of exposing it to callers.
/// </remarks>
sealed class BillingService
{
	/// <summary>
	/// Reserves a quota for the tenant.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>The reserved quota, or a <see cref="BillingError"/> describing why it could not be reserved.</returns>
	public Result<Quota, BillingError> ReserveQuota(TenantId tenantId) =>
		tenantId.Value == "globex"
			? new BillingServiceUnavailable("the billing service is offline").AsFailure<Quota>()
			: Result<Quota, BillingError>.Success(new Quota(tenantId, Seats: 5));
}
