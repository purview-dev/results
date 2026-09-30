using Purview.Results.ZodSharp;

namespace Purview.Results.Examples.AspNetCore.Zod;

/// <summary>
/// Registers tenants, validating every input with ZodSharp before it is used.
/// </summary>
sealed class TenantRegistrationService
{
	readonly Dictionary<TenantId, Tenant> _tenants = new()
	{
		[new TenantId("acme")] = new Tenant(new TenantId("acme"), "Acme", Enabled: true),
	};

	/// <summary>
	/// Registers a tenant from raw input, validating it first.
	/// </summary>
	/// <param name="input">The input to validate and register.</param>
	/// <returns>The registered tenant, or a <see cref="TenantError"/> describing why it was rejected.</returns>
	public Result<Tenant, TenantError> Register(TenantInput input) =>
		TenantInputSchema
			.Validate(input)
			.ToResult<TenantInput, TenantError>(errors => new TenantInputInvalid(input, errors))
			.Bind(RegisterValidated);

	Result<Tenant, TenantError> RegisterValidated(TenantInput input)
	{
		// Validation guarantees both members are present, so the non-null assertion is safe here.
		TenantId tenantId = new(input.TenantId!);

		if (_tenants.ContainsKey(tenantId))
			return new TenantAlreadyExists(tenantId).AsFailure<Tenant>();

		Tenant tenant = new(tenantId, input.Name!, Enabled: true);
		_tenants[tenantId] = tenant;

		return Result<Tenant, TenantError>.Success(tenant);
	}
}
