namespace Purview.Results.Examples.Basic;

/// <summary>
/// The expected failures of a tenant operation.
/// </summary>
/// <remarks>
/// The union itself is the error type of <c>Result&lt;Tenant, TenantError&gt;</c>. A bare case value cannot
/// convert to the result because C# never composes two user-defined conversions, so the generator emits one
/// <c>AsFailure&lt;TValue&gt;()</c> helper per case of every <c>[GenerateResult]</c> union.
/// </remarks>
[GenerateResult]
readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

/// <summary>
/// The requested tenant does not exist.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>
/// The requested tenant exists but is disabled.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantDisabled(TenantId TenantId);

/// <summary>
/// A tenant with the requested identifier already exists.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
readonly record struct TenantAlreadyExists(TenantId TenantId);
