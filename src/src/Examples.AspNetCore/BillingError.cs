namespace Purview.Results.Examples.AspNetCore;

/// <summary>
/// The expected failures of the billing service.
/// </summary>
/// <remarks>
/// This union belongs to the billing service boundary; the tenant billing lookup widens it into
/// <see cref="TenantOperationError"/> rather than leaking its case types to callers.
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
/// The expected failures of a tenant billing lookup: the tenant error union and the billing error union, each as
/// one case.
/// </summary>
/// <remarks>
/// The tenant union is nested, so a tenant failure resolves to its leaf case in the HTTP layer while a mapping
/// registered for <see cref="TenantError"/> still covers it.
/// </remarks>
[GenerateResult]
readonly union TenantOperationError(TenantError, BillingError);

/// <summary>
/// A tenant's billing account.
/// </summary>
/// <param name="TenantId">The tenant the account belongs to.</param>
/// <param name="AccountNumber">The account number.</param>
readonly record struct BillingAccount(TenantId TenantId, string AccountNumber);
