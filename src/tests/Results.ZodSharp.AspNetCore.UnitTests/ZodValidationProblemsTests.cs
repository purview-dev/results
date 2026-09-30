using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using global::ZodSharp;
using global::ZodSharp.AspNetCore;
using global::ZodSharp.Core;

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
		var response = await ExecuteAsync(
			ZodValidationProblems.ToProblem(errors, CreateOptions()),
			CreateContext()
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(response.ContentType).Contains("application/problem+json");
		await Assert.That(response.Body).Contains(InvalidNameCode);
		await Assert.That(response.Body).Contains("issues");
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
		var response = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), options),
			CreateContext()
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(response.Body).Contains("The name is not valid.");
	}

	[Test]
	public async Task ToProblem_GivenStatusCodeSelector_ReturnsItsStatus()
	{
		// Arrange
		ZodProblemDetailsOptions options = new()
		{
			StatusCodeSelector = _ => StatusCodes.Status409Conflict,
		};

		// Act
		var response = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), options),
			CreateContext()
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	[Test]
	public async Task ToProblem_GivenTraceId_IncludesItInTheExtensions()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var response = await ExecuteAsync(
			ZodValidationProblems.ToProblem(CreateErrors(), CreateOptions(), traceId: context.TraceIdentifier),
			context
		);

		// Assert
		await Assert.That(response.Body).Contains(context.TraceIdentifier);
	}

	[Test]
	public async Task ToProblem_GivenTheSameErrorsAShrownException_ProducesTheSameResponse()
	{
		// Arrange
		var errors = CreateErrors();
		ErrorTypeRegistry registry = new();
		ZodProblemDetailsOptions options = new() { Registry = registry };
		var context = CreateContext();

		// Act
		var resultResponse = await ExecuteAsync(
			ZodValidationProblems.ToProblem(errors, options),
			context
		);
		var exceptionResponse = await ExecuteAsync(
			new ZodException(errors).ToHttpValidationProblemDetails(registry, StatusCodes.Status400BadRequest),
			CreateContext()
		);

		// Assert
		await Assert.That(resultResponse.StatusCode).IsEqualTo(exceptionResponse.StatusCode);
		await Assert.That(resultResponse.Body).IsEqualTo(exceptionResponse.Body);
	}

	[Test]
	public async Task ToValidationProblem_GivenValidationErrorCarrier_ReturnsValidationProblem()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var response = await ExecuteAsync(
			new InputRejected(7, CreateErrors()).ToValidationProblem(context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(response.Body).Contains(InvalidNameCode);
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
		DefaultHttpContext context = new() { Response = { Body = new MemoryStream() } };

		if (services is not null)
			context.RequestServices = services;

		return context;
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
