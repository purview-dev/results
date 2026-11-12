namespace Purview.Results.Examples.Unions;

/// <summary>
/// A tenant lookup that reports its expected failures as a <see cref="TenantError"/> result.
/// </summary>
sealed class TenantDirectory
{
	readonly Dictionary<TenantId, Tenant> _tenants = new()
	{
		[new TenantId("acme")] = new Tenant(new TenantId("acme"), "Acme", Enabled: true),
		[new TenantId("globex")] = new Tenant(new TenantId("globex"), "Globex", Enabled: false),
	};

	/// <summary>
	/// Finds a tenant, or reports why it could not be used.
	/// </summary>
	/// <param name="id">The identifier of the tenant.</param>
	/// <returns>The tenant, or the union case describing the failure.</returns>
	/// <remarks>
	/// A bare case value cannot convert to the result because C# never composes two user-defined conversions:
	/// the case converts to the union, and the union converts to the result, but not in one sequence. The two
	/// generated forms this method uses are the per-case <c>AsFailure&lt;TValue&gt;()</c> helper and the
	/// union-receiver <c>TenantError.Failure&lt;TValue&gt;(...)</c> factory.
	/// </remarks>
	public Result<Tenant, TenantError> Find(TenantId id)
	{
		if (!_tenants.TryGetValue(id, out var tenant))
			return new TenantNotFound(id).AsFailure<Tenant>();

		// The tenant exists, but is disabled.
		return tenant.Enabled ? tenant : TenantError.Failure<Tenant>(new TenantDisabled(id));
	}

	/// <summary>
	/// Deletes a tenant, or reports that it did not exist.
	/// </summary>
	/// <param name="id">The identifier of the tenant.</param>
	/// <returns>A unit result: success carries no value, so only the failure has to be carried.</returns>
	public Result<TenantError> Delete(TenantId id) =>
		_tenants.Remove(id) ? Result<TenantError>.Success() : new TenantNotFound(id).AsFailure();
}
