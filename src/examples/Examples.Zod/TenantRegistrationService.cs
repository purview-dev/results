using Purview.Results.ZodSharp;

namespace Purview.Results.Examples.Zod;

/// <summary>
/// Registers tenants, validating every input with ZodSharp before it is used.
/// </summary>
/// <remarks>
/// <para>
/// <c>Validate</c> never throws: it returns a <c>ValidationResult&lt;TenantInput&gt;</c> carrying the input on
/// success and every <c>ValidationError</c> on failure. <c>ToResult</c> translates that into a result whose
/// error type this method names explicitly, because a union case does not carry the union that contains it.
/// </para>
/// <para>
/// Throwing stays reserved for exceptional circumstances — a lost database connection, for example. A rejected
/// input is an expected outcome, so it is a value.
/// </para>
/// </remarks>
public sealed class TenantRegistrationService
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
		// The validated value is the value this method succeeds with, so the adapter maps the failure into the
		// union case directly and Bind chains the registration that can fail again.
		TenantInputSchema
			.Validate(input)
			.ToResult<TenantInput, TenantError>(errors => new TenantInputInvalid(input, errors))
			.Bind(RegisterValidated);

	/// <summary>
	/// Registers a tenant from raw parts, using the generated helper because the validated value is not the
	/// value this method succeeds with.
	/// </summary>
	/// <param name="tenantId">The identifier of the new tenant.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The registered tenant, or a <see cref="TenantError"/> describing why it was rejected.</returns>
	public Result<Tenant, TenantError> Register(string? tenantId, string? name)
	{
		var input = TenantInput.Create(tenantId, name);
		var validation = TenantInputSchema.Validate(input);

		// The guard idiom: the method succeeds with a Tenant, so the failure is produced by the helper the
		// generator emits for the case rather than by the ToResult adapter.
		if (!validation.IsSuccess)
			return new TenantInputInvalid(input, validation.Errors).AsFailure<Tenant>();

		return RegisterValidated(validation.Value);
	}

	/// <summary>
	/// Finds a tenant.
	/// </summary>
	/// <param name="tenantId">The identifier of the tenant.</param>
	/// <returns>The tenant, or a <see cref="TenantNotFound"/> failure.</returns>
	public Result<Tenant, TenantError> Find(TenantId tenantId) =>
		_tenants.TryGetValue(tenantId, out var tenant)
			? Result<Tenant, TenantError>.Success(tenant)
			: new TenantNotFound(tenantId).AsFailure<Tenant>();

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
