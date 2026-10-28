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

// AddZodSharpBlazor registers the .NET 11 validation pipeline (AddZodSharpValidation), which validates a [ZodSchema]
// parameter before the handler runs and answers with its own 400 - so the result rules below would never see the
// failure. This endpoint's service validates and returns a result, so the pipeline is turned off for it.
app.MapPost(
		"/api/tenants",
		(TenantRegistration registration, TenantRegistrationService service) => service.Register(registration)
	)
	.WithResultsHttp()
	.DisableValidation()
	.DisableAntiforgery();

// The same endpoint with the pipeline left on, to show what a host that forgets gets: ZodSharp's 400, not the rule's 422.
app.MapPost(
		"/api/tenants/prevalidated",
		(TenantRegistration registration, TenantRegistrationService service) => service.Register(registration)
	)
	.WithResultsHttp()
	.DisableAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
