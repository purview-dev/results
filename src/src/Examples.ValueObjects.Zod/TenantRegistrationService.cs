namespace Purview.Results.Examples.ValueObjects.Zod;

/// <summary>
/// Registers tenants, validating every identifier through its value object's schema.
/// </summary>
sealed class TenantRegistrationService
{
	readonly Dictionary<TenantId, Tenant> _tenants = [];

	/// <summary>
	/// Registers a tenant from a raw identifier.
	/// </summary>
	/// <param name="tenantId">The raw identifier to validate and register.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The registered tenant, or a <see cref="TenantError"/> describing why it was rejected.</returns>
	public Result<Tenant, TenantError> Register(Guid tenantId, string name)
	{
		// Hydrate builds the value object without running its rule; Validate reports the rule instead of
		// throwing, so a rejected identifier is an expected outcome rather than an exception.
		var candidate = TenantId.Hydrate(tenantId);
		var validation = TenantIdSchema.Validate(candidate);

		if (!validation.IsSuccess)
			return new TenantInputInvalid(tenantId, validation.Errors).AsFailure<Tenant>();

		var id = validation.Value;

		if (_tenants.ContainsKey(id))
			return new TenantAlreadyExists(id).AsFailure<Tenant>();

		Tenant tenant = new(id, name, Enabled: true);
		_tenants[id] = tenant;

		return Result<Tenant, TenantError>.Success(tenant);
	}
}
