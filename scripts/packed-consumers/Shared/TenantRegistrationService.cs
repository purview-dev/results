// Shared by the Blazor consumers (copied in by scripts/test-packed-consumers.ps1).
using Purview.Results;
using Purview.Results.ZodSharp;

namespace PackedResultsBlazor;

/// <summary>Registers tenants: a rejected input and an existing tenant are values, not exceptions.</summary>
public sealed class TenantRegistrationService
{
	public const string ExistingTenantId = "acme";

	readonly HashSet<string> _tenants = new(StringComparer.Ordinal) { ExistingTenantId };

	/// <summary>Validates with the generated schema, then registers - through ToResult and Bind.</summary>
	public Result<Tenant, TenantError> Register(TenantRegistration registration) =>
		TenantRegistrationSchema
			.Validate(registration)
			.ToResult<TenantRegistration, TenantError>(errors => new TenantInputInvalid(errors))
			.Bind(Store);

	/// <summary>The asynchronous form: ValidateAsync folded through ToResultAsync.</summary>
	public async ValueTask<Result<Tenant, TenantError>> RegisterAsync(
		TenantRegistration registration,
		CancellationToken cancellationToken = default
	)
	{
		var validated = await TenantRegistrationSchema
			.ValidateAsync(registration, services: null, cancellationToken)
			.ToResultAsync<TenantRegistration, TenantError>(errors => new TenantInputInvalid(errors));

		return validated.Bind(Store);
	}

	Result<Tenant, TenantError> Store(TenantRegistration registration)
	{
		// The generated per-case helper: the case does not name the union, so AsFailure<TValue>() builds the result.
		if (!_tenants.Add(registration.TenantId!))
			return new TenantAlreadyExists(registration.TenantId!).AsFailure<Tenant>();

		return Result<Tenant, TenantError>.Success(
			new Tenant(registration.TenantId!, registration.Name!, registration.OwnerEmail!)
		);
	}
}
