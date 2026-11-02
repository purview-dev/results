using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.Results.AspNetCore;

public sealed class ResultHttpResultTests
{
	[Test]
	public async Task ToHttpResult_GivenSuccessfulResult_ReturnsOkWithTheValue()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/ok", () => Result<int, HttpTestError>.Success(7).ToHttpResult())
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/ok", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("7");
	}

	[Test]
	public async Task ToHttpResult_GivenMappedFailure_ReturnsTheMappedResponse()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/missing", () => new ItemNotFound(7).AsFailure<int>().ToHttpResult())
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/missing", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
		await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
	}

	[Test]
	public async Task ToHttpResult_GivenUninitializedResult_ReturnsTheUninitializedProblem()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/broken", () => default(Result<int, HttpTestError>).ToHttpResult())
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/broken", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
		await Assert.That(await response.Content.ReadAsStringAsync()).Contains("results_uninitialized");
	}

	[Test]
	public async Task ToHttpResult_GivenSuccessfulUnitResult_ReturnsNoContent()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapDelete("/items/{id:int}", (int id) => Result<HttpTestError>.Success().ToHttpResult())
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.DeleteAsync(new Uri("/items/7", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
		await Assert.That(await response.Content.ReadAsStringAsync()).IsEmpty();
	}

	[Test]
	public async Task ToHttpResult_GivenFailedUnitResult_ReturnsTheMappedResponse()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapDelete("/items/{id:int}", (int id) => new ItemNotFound(id).AsFailure().ToHttpResult())
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.DeleteAsync(new Uri("/items/7", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
	}

	[Test]
	public async Task ToHttpResult_GivenEndpointUnderWithResultsHttp_IsLeftToExecuteItself()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapGet("/missing", () => new ItemNotFound(7).AsFailure<int>().ToHttpResult()).WithResultsHttp()
		);
		using var client = app.GetTestClient();

		// Act
		var response = await client.GetAsync(new Uri("/missing", UriKind.Relative));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
	}

	[Test]
	public async Task Result_GivenAWrappedResult_ReturnsIt()
	{
		// Arrange
		var result = Result<int, HttpTestError>.Success(7);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		await Assert.That(httpResult.Result).IsEqualTo(result);
	}

	[Test]
	public async Task ExecuteAsync_GivenNoRegisteredMapper_ThrowsNamingTheMissingRegistration()
	{
		// Arrange
		var context = ResultsHttpTestFactory.CreateContext(new ServiceCollection().BuildServiceProvider());
		var httpResult = Result<int, HttpTestError>.Success(7).ToHttpResult();

		// Act & Assert
		var exception = await Assert
			.That(async () => await httpResult.ExecuteAsync(context))
			.Throws<InvalidOperationException>();
		await Assert.That(exception!.Message).Contains("AddResultsHttp()");
	}

	[Test]
	public async Task ExecuteAsync_GivenNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var httpResult = Result<HttpTestError>.Success().ToHttpResult();

		// Act & Assert
		await Assert.That(async () => await httpResult.ExecuteAsync(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task PopulateMetadata_GivenValueResult_DescribesTheValueWithTheSuccessStatusCode()
	{
		// Arrange
		await using var app = await CreateHostAsync(
			app => app.MapGet("/ok", () => Result<int, HttpTestError>.Success(7).ToHttpResult()),
			options => options.SuccessStatusCode = StatusCodes.Status201Created
		);

		// Act
		var produces = GetProducesMetadata(app);

		// Assert
		await Assert.That(produces.Count).IsEqualTo(1);
		await Assert.That(produces[0].StatusCode).IsEqualTo(StatusCodes.Status201Created);
		await Assert.That(produces[0].Type).IsEqualTo(typeof(int));
		await Assert.That(produces[0].ContentTypes).IsEquivalentTo(["application/json"]);
	}

	[Test]
	public async Task PopulateMetadata_GivenSuccessMapper_DescribesNothing()
	{
		// Arrange
		await using var app = await CreateHostAsync(
			app => app.MapGet("/ok", () => Result<int, HttpTestError>.Success(7).ToHttpResult()),
			options => options.SuccessMapper = (_, _) => TypedResults.NoContent()
		);

		// Act
		var produces = GetProducesMetadata(app);

		// Assert
		await Assert.That(produces).IsEmpty();
	}

	[Test]
	public async Task PopulateMetadata_GivenUnitResult_DescribesNoContent()
	{
		// Arrange
		await using var app = await CreateHostAsync(app =>
			app.MapDelete("/items/{id:int}", (int id) => Result<HttpTestError>.Success().ToHttpResult())
		);

		// Act
		var produces = GetProducesMetadata(app);

		// Assert
		await Assert.That(produces.Count).IsEqualTo(1);
		await Assert.That(produces[0].StatusCode).IsEqualTo(StatusCodes.Status204NoContent);
		await Assert.That(produces[0].Type).IsNull();
	}

	static List<IProducesResponseTypeMetadata> GetProducesMetadata(WebApplication app) =>
		[
			.. ((IEndpointRouteBuilder)app)
				.DataSources.SelectMany(static source => source.Endpoints)
				.Single()
				.Metadata.OfType<IProducesResponseTypeMetadata>(),
		];

	static async Task<WebApplication> CreateHostAsync(
		Action<WebApplication> configure,
		Action<ResultsHttpOptions>? configureOptions = null
	)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();

		builder.Services.AddResultsHttp(options =>
		{
			options.Map<ItemNotFound>(notFound =>
				TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Item {notFound.ItemId}.")
			);
			configureOptions?.Invoke(options);
		});

		var app = builder.Build();

		configure(app);

		await app.StartAsync();

		return app;
	}
}
