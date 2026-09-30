// Demonstrates Purview.Results.ZodSharp.AspNetCore: a result failure that carries ZodSharp validation errors is
// rendered as an ASP.NET Core HttpValidationProblemDetails response, produced by the same mapper the ZodSharp
// exception handler uses, so a validation failure carried by a result and the same failure thrown as a
// ZodException produce identical responses.
//
//   dotnet run --project src/examples/Examples.AspNetCore.Zod --urls http://localhost:5216
//
//   curl -i -X POST http://localhost:5216/tenants -H "Content-Type: application/json" -d "{\"tenantId\":\"newco\",\"name\":\"Newco\"}"       -> 200 OK
//   curl -i -X POST http://localhost:5216/tenants -H "Content-Type: application/json" -d "{\"name\":\"Nameless\"}"                        -> 400 validation problem
//   curl -i -X POST http://localhost:5216/tenants -H "Content-Type: application/json" -d "{\"tenantId\":\"same\",\"name\":\"same\"}"      -> 422 Unprocessable Entity   (code rule)
//   curl -i -X POST http://localhost:5216/tenants -H "Content-Type: application/json" -d "{\"tenantId\":\"acme\",\"name\":\"Acme\"}"       -> 409 Conflict
//
// The 422 shows the code rule: a failure whose errors include `tenant_id_matches_name` is answered by the rule
// registered below, while every other validation failure stays a 400 validation problem.
//
// The 409 shows the precedence: the mapping registered for a specific case always wins over the validation
// mapping, and a mapping registered for the error type would still win over it too.

var builder = WebApplication.CreateBuilder(args);

// The problem mapper, exception handler and options the ZodSharp packages share.
builder.Services.AddZodSharpProblemDetails();

builder.Services.AddResultsHttp(options =>
	options.Map<TenantAlreadyExists>(error =>
		TypedResults.Problem(
			statusCode: StatusCodes.Status409Conflict,
			title: "The tenant already exists.",
			detail: $"Tenant '{error.TenantId.Value}' already exists."
		)
	)
);

// Registered as a failure mapper, so the case mapping above still wins and a failure that carries no validation
// errors still takes the host's unmapped-failure path. A code rule answers a failure whose errors include the
// code with a response of its own; every other validation failure stays a 400 validation problem.
builder.Services.AddResultsZodSharpHttp(options =>
	options.MapCode("tenant_id_matches_name", StatusCodes.Status422UnprocessableEntity)
);

var app = builder.Build();

TenantRegistrationService service = new();

app.MapPost(
		"/tenants",
		(TenantRegistrationRequest request) => service.Register(TenantInput.Create(request.TenantId, request.Name))
	)
	.WithResultsHttp();

app.Run();

/// <summary>
/// The JSON body the tenant registration endpoint accepts.
/// </summary>
/// <param name="TenantId">The identifier of the new tenant.</param>
/// <param name="Name">The display name of the new tenant.</param>
sealed record TenantRegistrationRequest(string? TenantId, string? Name);
