// Packed-consumer gate, consumer (e): a Native AOT minimal API built from the packages alone. The gate publishes it,
// starts it, and calls each endpoint; a wrong status, body or content type fails the gate.
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PackedResultsAotWeb;
using PackedResultsBlazor;
using Purview.Results;
using Purview.Results.ZodSharp;

var builder = WebApplication.CreateSlimBuilder(args);

// Native AOT has no reflection-based JSON metadata, so the request and response types come from the app's own context.
// The mapper writes a success as Ok<Tenant> (or Json<Tenant> for another status) with the value's static type, which
// is what lets this context resolve it; the problem responses resolve from ASP.NET Core's and ZodSharp's own contexts.
builder.Services.ConfigureHttpJsonOptions(options =>
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, PackedResultsAotWebJsonContext.Default)
);

builder.Services.AddZodSharpPackedResultsAotWebSchemas();
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddResultsHttp(options =>
{
	// Started twice by the gate: once with the default 200 (TypedResults.Ok) and once with --SuccessStatusCode=201,
	// which takes the TypedResults.Json path. Parsed by hand: the configuration binder needs reflection.
	if (int.TryParse(builder.Configuration["SuccessStatusCode"], out var successStatusCode))
		options.SuccessStatusCode = successStatusCode;

	options.Map<TenantAlreadyExists>(error =>
		TypedResults.Problem(
			statusCode: StatusCodes.Status409Conflict,
			title: $"Tenant '{error.TenantId}' already exists."
		)
	);
});
builder.Services.AddResultsZodSharpHttp(options =>
	options.MapCode(TenantRegistration.TenantIdMatchesNameCode, StatusCodes.Status422UnprocessableEntity)
);
builder.Services.AddSingleton<TenantRegistrationService>();

var app = builder.Build();

// Every handler returns ToHttpResult(), never the result itself: under Native AOT the request delegate generator
// resolves JSON metadata for a handler's declared return type when the endpoint is built, and a Result<...> whose error
// is a union has none. ResultHttpResult is an IResult, so only the value type (Tenant) needs metadata.
var tenants = app.MapGroup("/api/tenants");

// A value result: the validation problem, the code rule's 422, the case mapping's 409, or the tenant.
tenants.MapPost(
	"/",
	(TenantRegistration registration, TenantRegistrationService service) =>
		service.Register(registration).ToHttpResult()
);

// A unit result: a success carries no payload, so it answers 204 No Content.
tenants.MapDelete("/{tenantId}", (string tenantId) => Result<TenantError>.Success().ToHttpResult());

// An uninitialized result is a bug in the endpoint: 500 with the results_uninitialized discriminator.
tenants.MapGet("/uninitialized", () => default(Result<Tenant, TenantError>).ToHttpResult());

// The client side, also under Native AOT: Purview.ZodSharp.Http validates a request before sending it and reads the
// server's validation problem back into ZodSharp errors, and both fold into a result through Purview.Results.ZodSharp.
app.MapGet(
	"/client-self-test",
	// [FromServices]: the request delegate generator otherwise treats an interface parameter as a possible JSON body
	// and resolves metadata for it, which Native AOT does not have.
	async (HttpContext context, [FromServices] IServiceProvider services) =>
	{
		// The generated validator adapter (an IZodSchemaValidator<T>); AddZodSharp{Assembly}Schemas() registers it in the
		// IZodSchemaFactory rather than as a service, so it is created directly.
		TenantRegistrationSchemaValidator validator = new();
		using HttpClient client = new() { BaseAddress = new Uri($"{context.Request.Scheme}://{context.Request.Host}") };
		var jsonTypeInfo = PackedResultsAotWebJsonContext.Default.TenantRegistration;
		StringBuilder report = new();
		var failed = false;

		void Check(bool condition, string description)
		{
			failed |= !condition;
			report.AppendLine($"{(condition ? "PASS" : "FAIL")} {description}");
		}

		// Rejected locally: nothing is sent, and the errors become the union case.
		TenantRegistration invalid = new()
		{
			TenantId = "Not Valid!",
			Name = "x",
			OwnerEmail = "nope",
		};
		var notSent = await client
			.PostAsJsonValidatedAsync("/api/tenants", invalid, validator, jsonTypeInfo, services)
			.ToResultAsync<HttpResponseMessage, TenantError>(errors => new TenantInputInvalid(errors));
		Check(
			notSent.TryGetError(out var localError) && localError is TenantInputInvalid { Errors.Length: 3 },
			"PostAsJsonValidatedAsync folds a locally rejected request into the union case through ToResultAsync"
		);

		// Rejected by the server: the validation problem it writes reads back into the same case.
		using var rejected = await client.PostAsJsonAsync("/api/tenants", invalid, jsonTypeInfo);
		var problem = await rejected.TryReadZodValidationProblemAsync();
		Result<Tenant, TenantError> serverVerdict = problem is null
			? Result<Tenant, TenantError>.Success(new Tenant("", "", ""))
			: new TenantInputInvalid(problem.ToValidationErrors()).AsFailure<Tenant>();
		Check(
			serverVerdict.TryGetError(out var serverError)
				&& serverError is TenantInputInvalid { Errors: var serverErrors }
				&& serverErrors
					.Select(static error => error.FullPath.ToDottedString())
					.Order()
					.SequenceEqual(["Name", "OwnerEmail", "TenantId"]),
			"TryReadZodValidationProblemAsync reads the server's validation problem back into the union case"
		);

		// Accepted: validated locally, sent, and answered with the tenant.
		TenantRegistration valid = new()
		{
			TenantId = "clientco",
			Name = "Clientco",
			OwnerEmail = "owner@example.com",
		};
		var sent = await client
			.PostAsJsonValidatedAsync("/api/tenants", valid, validator, jsonTypeInfo, services)
			.ToResultAsync<HttpResponseMessage, TenantError>(errors => new TenantInputInvalid(errors));
		Check(sent.IsSuccess && sent.Value.IsSuccessStatusCode, "a valid request is sent and accepted");
		if (sent.IsSuccess)
			sent.Value.Dispose();

		return failed
			? Results.Text(report.ToString(), statusCode: StatusCodes.Status500InternalServerError)
			: Results.Text(report.ToString());
	}
);

await app.RunAsync();

namespace PackedResultsAotWeb
{
	/// <summary>The app's JSON metadata: the request body and the successful value.</summary>
	[JsonSerializable(typeof(TenantRegistration))]
	[JsonSerializable(typeof(Tenant))]
	internal sealed partial class PackedResultsAotWebJsonContext : JsonSerializerContext;
}
