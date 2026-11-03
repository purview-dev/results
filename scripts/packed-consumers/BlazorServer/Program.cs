using PackedResultsBlazor;
using PackedResultsServer.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// ZodSharp for Blazor forms and ProblemDetails, then the result-to-HTTP mapping: a case mapping for an existing
// tenant, and a code rule for the refinement. Every other validation failure becomes the standard validation problem.
builder.Services.AddZodSharpPackedResultsServerSchemas();
builder.Services.AddZodSharpBlazor();
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddResultsHttp(options =>
	options.Map<TenantAlreadyExists>(error =>
		TypedResults.Problem(
			statusCode: StatusCodes.Status409Conflict,
			title: $"Tenant '{error.TenantId}' already exists."
		)
	)
);
builder.Services.AddResultsZodSharpHttp(options =>
	options.MapCode(TenantRegistration.TenantIdMatchesNameCode, StatusCodes.Status422UnprocessableEntity)
);
builder.Services.AddSingleton<TenantRegistrationService>();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();

// The service validates and returns a result, which WithResultsHttp maps. AddZodSharpBlazor validates form models only
// and does not turn on minimal-API parameter validation, so the handler always runs and the result rules above answer:
// the refinement's 422, the existing tenant's 409, and the standard validation problem for everything else.
app.MapPost(
		"/api/tenants",
		(TenantRegistration registration, TenantRegistrationService service) => service.Register(registration)
	)
	.WithResultsHttp()
	.DisableAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
