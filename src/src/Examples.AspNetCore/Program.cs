// Demonstrates Purview.Results.AspNetCore: an endpoint returns Result<Tenant, TenantError> and the host decides
// what each error case looks like on the wire.
//
//   dotnet run --project src/src/Examples.AspNetCore --urls http://localhost:5215
//
//   curl -i http://localhost:5215/tenants/acme             -> 200 OK
//   curl -i http://localhost:5215/tenants/initech          -> 404 Not Found          (case mapping)
//   curl -i http://localhost:5215/tenants/globex/usage     -> 403 Forbidden          (case mapping)
//   curl -i -X POST http://localhost:5215/tenants/newco    -> 200 OK
//   curl -i -X POST http://localhost:5215/tenants/acme     -> 409 Conflict           (error mapping)
//   curl -i http://localhost:5215/tenants/acme/billing     -> 503 Service Unavailable (nested union leaf mapping)
//   curl -i http://localhost:5215/tenants/hooli/billing    -> 402 Payment Required    (nested union leaf mapping)
//   curl -i http://localhost:5215/tenants/initech/billing  -> 404 Not Found           (nested tenant error leaf)
//   curl -i http://localhost:5215/tenants/globex/billing   -> 403 Forbidden           (nested tenant error leaf)
//   curl -i -X DELETE http://localhost:5215/tenants/hooli   -> 204 No Content          (unit result success)
//   curl -i -X DELETE http://localhost:5215/tenants/initech -> 404 Not Found           (unit result failure)
//   curl -i http://localhost:5215/tenants/broken            -> 500 Internal Server Error
//
// The billing endpoint returns Result<BillingAccount, TenantOperationError>, whose error union nests TenantError
// and BillingError. The mapper resolves the innermost case, so the billing leaf mappings win and the mapping
// registered for TenantError still covers a nested tenant failure.
//
// The delete endpoint returns a unit Result<TenantError>: it has no value on success, so the mapper answers 204
// No Content, while a missing tenant still flows through the same case mapping as every other failure.
//
// The last one is deliberate: an endpoint returning `default` is a host bug, so it takes the unmapped-failure
// path and is logged rather than being coerced into a success or a failure.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResultsHttp(options =>
	options
		// A mapping for the case type wins over the mapping for the error type.
		.Map<TenantNotFound>(error =>
			TypedResults.NotFound(new { error = nameof(TenantNotFound), tenantId = error.TenantId.Value })
		)
		.Map<TenantDisabled>(error =>
			TypedResults.Problem(
				statusCode: StatusCodes.Status403Forbidden,
				title: "The tenant is disabled.",
				detail: $"Tenant '{error.TenantId.Value}' is disabled."
			)
		)
		// The mapping for the error type covers every case that has no mapping of its own.
		.Map<TenantError>(_ =>
			TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The tenant already exists.")
		)
		// A mapping for the leaf of a nested union wins, so these handle the billing service's failures while the
		// mapping for TenantError above still covers a nested tenant failure.
		.Map<BillingServiceUnavailable>(error =>
			TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: error.Reason)
		)
		.Map<BillingAccountMissing>(error =>
			TypedResults.Problem(
				statusCode: StatusCodes.Status402PaymentRequired,
				title: $"No billing account for '{error.TenantId.Value}'."
			)
		)
);

var app = builder.Build();

InMemoryTenantStore store = new();
TenantBillingService billing = new(store, new BillingService());

app.MapGet("/tenants/{id}", (string id) => store.GetTenant(new TenantId(id))).WithResultsHttp();

app.MapGet("/tenants/{id}/usage", (string id) => store.GetEnabledTenant(new TenantId(id))).WithResultsHttp();

app.MapGet("/tenants/{id}/billing", (string id) => billing.GetAccount(new TenantId(id))).WithResultsHttp();

app.MapPost("/tenants/{id}", (string id) => store.CreateTenant(new TenantId(id), id)).WithResultsHttp();

// A unit result has no value on success, so the mapper answers 204 No Content; its failure still maps by case.
app.MapDelete("/tenants/{id}", (string id) => store.DeleteTenant(new TenantId(id))).WithResultsHttp();

app.MapGet("/tenants/broken", () => default(Result<Tenant, TenantError>)).WithResultsHttp();

app.Run();
