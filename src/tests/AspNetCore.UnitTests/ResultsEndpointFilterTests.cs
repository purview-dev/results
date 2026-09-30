using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.Results.AspNetCore;

public sealed class ResultsEndpointFilterTests
{
	[Test]
	public async Task Endpoint_GivenSuccessfulResult_ReturnsOk()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/ok", () => Result<int, HttpTestError>.Success(7)).WithResultsHttp()
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/ok", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync()).Contains("7");
	}

	[Test]
	public async Task Endpoint_GivenSuccessfulResultFromAnAsyncHandler_ReturnsOk()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/async", () => Task.FromResult(Result<int, HttpTestError>.Success(7))).WithResultsHttp()
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/async", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync()).Contains("7");
	}

	[Test]
	public async Task Endpoint_GivenMappedFailure_ReturnsTheMappedResponse()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/missing", () => new ItemNotFound(7).AsFailure<int>()).WithResultsHttp()
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/missing", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
		await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
	}

	[Test]
	public async Task Endpoint_GivenUnmappedFailure_ReturnsAnInternalServerErrorProblem()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/rejected", () => new ItemRejected(7).AsFailure<int>()).WithResultsHttp()
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/rejected", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
		await Assert.That(await response.Content.ReadAsStringAsync()).Contains(nameof(ItemRejected));
	}

	[Test]
	public async Task Endpoint_GivenHandlerThatDoesNotReturnAResult_LeavesTheResponseUntouched()
	{
		// Arrange
		await using var app = await CreateHostAsync(app => app.MapGet("/text", () => "hello").WithResultsHttp());
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/text", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("hello");
	}

	[Test]
	public async Task EndpointGroup_GivenResultsHttpMapping_MapsEveryEndpointInTheGroup()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
		{
			var group = app.MapGroup("/items").WithResultsHttp();
			group.MapGet("/{id:int}", (int id) => new ItemNotFound(id).AsFailure<ItemNotFound>());
		});
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/items/7", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
	}

	static async Task<WebApplication> CreateHostAsync(Action<WebApplication> configure)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();

		builder.Services.AddResultsHttp(options =>
			options.Map<ItemNotFound>(notFound =>
				TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Item {notFound.ItemId}.")
			)
		);

		var app = builder.Build();

		configure(app);

		await app.StartAsync();

		return app;
	}
}
