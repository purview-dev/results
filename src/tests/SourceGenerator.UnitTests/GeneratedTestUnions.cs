namespace Purview.Results.SourceGenerator;

// Union declarations used by the generated-code runtime tests. They live in the test project so the
// generator runs against a real compilation and the generated helpers are executed, not just compiled.

/// <summary>The identifier of a tenant.</summary>
/// <param name="Value">The identifier value.</param>
public readonly record struct TenantId(string Value);

/// <summary>The tenant that was requested does not exist.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>The tenant that was requested is disabled.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantDisabled(TenantId TenantId);

/// <summary>The tenant that was requested already exists.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantAlreadyExists(TenantId TenantId);

/// <summary>A tenant.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct Tenant(TenantId TenantId);

/// <summary>The errors a tenant operation can produce.</summary>
/// <remarks>
/// CA1815 is not raised for this declaration: the package's suppressor answers it for every union opted in
/// with <c>[GenerateResult]</c>, so no pragma or suppress-message attribute is needed here.
/// </remarks>
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

/// <summary>A billing account does not exist.</summary>
/// <param name="AccountId">The identifier of the billing account.</param>
public readonly record struct BillingAccountMissing(string AccountId);

/// <summary>The billing service is unavailable.</summary>
/// <param name="Reason">The reason the billing service is unavailable.</param>
public readonly record struct BillingServiceUnavailable(string Reason);

/// <summary>The errors a billing operation can produce.</summary>
/// <remarks>
/// This union stands in for a service boundary: a tenant operation consumes it and widens its error into
/// <see cref="RegisterTenantError"/> rather than leaking the billing case types.
/// </remarks>
[GenerateResult]
public readonly union BillingError(BillingAccountMissing, BillingServiceUnavailable);

/// <summary>
/// The errors a tenant registration can produce: the whole <see cref="TenantError"/> and
/// <see cref="BillingError"/> unions, each as a single case.
/// </summary>
/// <remarks>
/// The case types are unions, not their leaf cases. Each is an <em>included union</em>: the generated factory
/// also covers its cases and constructs the nested value, so a billing failure can be lifted into this contract
/// with one union conversion and no leaf case is shared with <see cref="BillingError"/>.
/// </remarks>
[GenerateResult]
public readonly union RegisterTenantError(TenantError, BillingError);
