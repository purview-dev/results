using Purview.Results.ZodSharp;

namespace Purview.Results.Examples.Blazor.Server;

/// <summary>
/// A tenant directory the Blazor sample runs entirely in the browser.
/// </summary>
static class TenantDirectory
{
	static readonly HashSet<string> Existing = [with(StringComparer.OrdinalIgnoreCase), "acme"];

	/// <summary>
	/// Validates a submitted tenant and folds the validation outcome into a result.
	/// </summary>
	/// <param name="input">The submitted form.</param>
	/// <returns>The registered tenant, or the union case describing why it was rejected.</returns>
	/// <remarks>
	/// <c>ToResult</c> carries the validation errors into <see cref="TenantInputInvalid"/>, and the generated
	/// <c>AsFailure&lt;TValue&gt;()</c> helper turns a domain case into the same result type. No exception is
	/// thrown for either an invalid form or an existing tenant; both are ordinary values the component renders.
	/// </remarks>
	public static Result<Tenant, TenantError> Register(TenantInput input) =>
		TenantInputSchema
			.Validate(input)
			.ToResult<TenantInput, TenantError>(errors => new TenantInputInvalid(errors))
			.Bind(valid =>
				Existing.Contains(valid.TenantId!)
					? new TenantAlreadyExists(new TenantId(valid.TenantId!)).AsFailure<Tenant>()
					: (Result<Tenant, TenantError>)new Tenant(new TenantId(valid.TenantId!), valid.Name!)
			);

	/// <summary>
	/// Looks up a tenant by identifier.
	/// </summary>
	/// <param name="tenantId">The identifier to look up.</param>
	/// <returns>The tenant, or <see cref="TenantNotFound"/> as a result failure.</returns>
	public static Result<Tenant, TenantError> Find(string tenantId)
	{
		TenantId id = new(tenantId);

		return Existing.Contains(tenantId)
			? (Result<Tenant, TenantError>)new Tenant(id, tenantId.ToUpperInvariant())
			: new TenantNotFound(id).AsFailure<Tenant>();
	}
}
