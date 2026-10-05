namespace Purview.Results.Examples.Basic;

/// <summary>
/// The expected failures of the billing service.
/// </summary>
/// <remarks>
/// This union belongs to the billing service boundary. The registration service widens it into
/// <see cref="RegisterTenantError"/> rather than leaking its case types to callers.
/// </remarks>
[GenerateResult]
readonly union BillingError(BillingAccountMissing, BillingServiceUnavailable);

/// <summary>
/// A billing account does not exist.
/// </summary>
/// <param name="TenantId">The tenant whose billing account is missing.</param>
readonly record struct BillingAccountMissing(TenantId TenantId);

/// <summary>
/// The billing service is unavailable.
/// </summary>
/// <param name="Reason">The reason the service is unavailable.</param>
readonly record struct BillingServiceUnavailable(string Reason);

/// <summary>
/// The expected failures of tenant registration: the tenant operation's own error union and the billing
/// service's error union, each as one case.
/// </summary>
/// <remarks>
/// The case types are the two unions rather than their leaf cases, so a billing failure lifts into this
/// contract with one union conversion and no case type is shared with <see cref="BillingError"/> (which would
/// raise <c>RSG1006</c>).
/// </remarks>
[GenerateResult]
readonly union RegisterTenantError(TenantError, BillingError);

/// <summary>
/// A billing quota reserved for a tenant.
/// </summary>
/// <param name="TenantId">The tenant the quota belongs to.</param>
/// <param name="Seats">The number of seats reserved.</param>
readonly record struct Quota(TenantId TenantId, int Seats);
