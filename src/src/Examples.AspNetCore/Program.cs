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
//   curl -i http://localhost:5215/tenants/broken           -> 500 Internal Server Error
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
);

var app = builder.Build();

InMemoryTenantStore store = new();

app.MapGet("/tenants/{id}", (string id) => store.GetTenant(new TenantId(id))).WithResultsHttp();

app.MapGet("/tenants/{id}/usage", (string id) => store.GetEnabledTenant(new TenantId(id))).WithResultsHttp();

app.MapPost("/tenants/{id}", (string id) => store.CreateTenant(new TenantId(id), id)).WithResultsHttp();

app.MapGet("/tenants/broken", () => default(Result<Tenant, TenantError>)).WithResultsHttp();

app.Run();
