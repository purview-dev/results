using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Purview.Results.AspNetCore;
using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

public sealed class ZodSharpResultsHttpRegistrationTests
{
	const string InvalidNameCode = "invalid_name";

	[Test]
	public async Task Map_GivenValidationErrorCarrier_ReturnsValidationProblemThroughTheFallback()
	{
		// Arrange
		await using var services = CreateServices();
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (StatusCode, Body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(Body).Contains(InvalidNameCode);
		await Assert.That(Body).Contains(context.TraceIdentifier);
	}

	[Test]
	public async Task Map_GivenErrorThatIsNotAValidationCarrier_ReturnsTheUnmappedProblem()
	{
		// Arrange
		await using var services = CreateServices();
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (StatusCode, Body) = await ExecuteAsync(mapper.Map(new ItemMissing(7).AsFailure<int>(), context), context);

		// Assert
		await Assert.That(StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
		await Assert.That(Body).Contains(nameof(ItemMissing));
	}

	[Test]
	public async Task Map_GivenMappingForTheCase_ReturnsTheMappedResponseInsteadOfTheFallback()
	{
		// Arrange
		await using var services = CreateServices(options =>
			options.Map<InputRejected>(rejected =>
				TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"rejected {rejected.ItemId}")
			)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (StatusCode, Body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(Body).Contains("rejected 7");
	}

	[Test]
	public async Task Map_GivenAggregateRejectedCase_ReturnsValidationProblem()
	{
		// Arrange
		await using var services = CreateServices();
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (StatusCode, Body) = await ExecuteAsync(
			mapper.Map(new AggregateRejected("operation", CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(Body).Contains(InvalidNameCode);
	}

	static ServiceProvider CreateServices(Action<ResultsHttpOptions>? configure = null)
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddResultsHttp(configure);
		services.AddResultsZodSharpHttp();

		return services.BuildServiceProvider();
	}

	static ImmutableArray<ValidationError> CreateErrors() =>
		[ValidationError.Create(InvalidNameCode, "The name is required.", ["Name"])];

	static DefaultHttpContext CreateContext(IServiceProvider services)
	{
		DefaultHttpContext context = new()
		{
			Response = { Body = new MemoryStream() },
			RequestServices = services
		};

		return context;
	}

	static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result, HttpContext context)
	{
		await result.ExecuteAsync(context);

		context.Response.Body.Seek(0, SeekOrigin.Begin);
		using StreamReader reader = new(context.Response.Body);
		var body = await reader.ReadToEndAsync();

		return (context.Response.StatusCode, body);
	}
}
