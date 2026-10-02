using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Text.Json;
using ZodSharp;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

public sealed class ZodValidationProblemsTests
{
	const string InvalidNameCode = "invalid_name";

	[Test]
	public async Task ToProblem_GivenValidationErrors_ReturnsValidationProblemWithTheIssuesExtension()
	{
		// Arrange
		var errors = CreateErrors();

		// Act
		var (statusCode, body, contentType) = await ExecuteAsync(
			ZodValidationProblems.ToProblem(errors, CreateOptions()),
			CreateContext()
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(contentType).Contains("application/problem+json");
		await Assert.That(body).Contains(InvalidNameCode);
		await Assert.That(body).Contains("issues");
	}

	[Test]
	public async Task ToProblem_GivenRegisteredErrorType_ReturnsItsStatusAndTitle()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType(
				Code: InvalidNameCode,
				Category: "invalid_value",
				Description: "The name is not valid.",
				HttpStatus: StatusCodes.Status422UnprocessableEntity,
				MessageFormat: "The name '{Name}' is not valid.",
				Parameters: [ErrorType.Param<string>("Name")]
			)
		);
		ZodProblemDetailsOptions options = new() { Registry = registry };

		// Act
		var (statusCode, body, _) = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), options),
			CreateContext()
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains("The name is not valid.");
	}

	[Test]
	public async Task ToProblem_GivenStatusCodeSelector_ReturnsItsStatus()
	{
		// Arrange
		ZodProblemDetailsOptions options = new() { StatusCodeSelector = _ => StatusCodes.Status409Conflict };

		// Act
		var (statusCode, body, contentType) = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), options),
			CreateContext()
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	[Test]
	public async Task ToProblem_GivenTraceId_IncludesItInTheExtensions()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var (_, body, _) = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), CreateOptions(), traceId: context.TraceIdentifier),
			context
		);

		// Assert
		await Assert.That(body).Contains(context.TraceIdentifier);
	}

	[Test]
	public async Task ToProblem_GivenTheSameErrorsAShownException_ProducesTheSameResponseFields()
	{
		// Arrange
		var errors = CreateErrors();
		ErrorTypeRegistry registry = new();
		ZodProblemDetailsOptions options = new() { Registry = registry };
		var context = CreateContext();

		// Act
		var (statusCode,
			body, contentType) = await ExecuteAsync(
			ZodValidationProblems.ToProblem(errors, options),
			context
		);
		var exceptionProblem = new ZodException(errors).ToHttpValidationProblemDetails(
			registry,
			StatusCodes.Status400BadRequest
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(exceptionProblem.Status);
		await Assert.That(contentType).Contains("application/problem+json");

		using var document = JsonDocument.Parse(body);
		var root = document.RootElement;

		await Assert.That(root.GetProperty("title").GetString()).IsEqualTo(exceptionProblem.Title);
		await Assert
			.That(root.GetProperty("errors").GetProperty("Name")[0].GetString())
			.IsEqualTo(exceptionProblem.Errors["Name"][0]);
		await Assert.That(root.TryGetProperty("issues", out var issues)).IsTrue();
		await Assert.That(issues.GetArrayLength()).IsEqualTo(1);
	}

	[Test]
	public async Task ToValidationProblem_GivenValidationErrorCarrier_ReturnsValidationProblem()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var (statusCode, body, _) = await ExecuteAsync(
			new InputRejected(7, CreateErrors()).ToValidationProblem(context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task ToValidationProblem_GivenTraceIdDisabled_OmitsTheTraceIdentifier()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddLogging();
		services.AddResultsHttp(options => options.IncludeTraceId = false);
		await using var provider = services.BuildServiceProvider();

		var context = CreateContext(provider);

		// Act
		var (statusCode, body, contentType) = await ExecuteAsync(
			new InputRejected(7, CreateErrors()).ToValidationProblem(context),
			context
		);

		// Assert
		await Assert.That(body).DoesNotContain(context.TraceIdentifier);
	}

	static ZodProblemDetailsOptions CreateOptions() => new();

	static ImmutableArray<ValidationError> CreateErrors() =>
		[
			ValidationError.Create(
				InvalidNameCode,
				"The name is required.",
				["Name"],
				new Dictionary<string, object?> { ["Name"] = "operation" }
			),
		];

	static DefaultHttpContext CreateContext(IServiceProvider? services = null)
	{
		DefaultHttpContext context = new()
		{
			Response = { Body = new MemoryStream() },
			RequestServices = services ?? CreateServices(),
		};

		return context;
	}

	static ServiceProvider CreateServices()
	{
		ServiceCollection services = new();
		services.AddOptions();
		services.AddLogging();

		return services.BuildServiceProvider();
	}

	static async Task<(int StatusCode, string Body, string? ContentType)> ExecuteAsync(
		IResult result,
		HttpContext context
	)
	{
		await result.ExecuteAsync(context);

		context.Response.Body.Seek(0, SeekOrigin.Begin);
		using StreamReader reader = new(context.Response.Body);
		var body = await reader.ReadToEndAsync();

		return (context.Response.StatusCode, body, context.Response.ContentType);
	}
}
