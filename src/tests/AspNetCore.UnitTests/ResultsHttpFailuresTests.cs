using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

public sealed class ResultsHttpFailuresTests
{
	[Test]
	public async Task Map_GivenUnmappedCase_ReturnsAnInternalServerErrorProblem()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
		await Assert.That(response.Body).Contains(typeof(ItemNotFound).FullName!);
		await Assert.That(response.Body).Contains("traceId");
		await Assert.That(response.ContentType).Contains("application/problem+json");
	}

	[Test]
	public async Task Map_GivenConfiguredUnmappedStatusCode_ReturnsThatStatus()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.UnmappedStatusCode = StatusCodes.Status502BadGateway
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status502BadGateway);
	}

	[Test]
	public async Task Map_GivenUnmappedCaseAndThrowEnabled_Throws()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options => options.ThrowOnUnmappedFailure = true);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var exception = await Assert
			.That(() => mapper.Map(new ItemNotFound(7).AsFailure<int>(), context))
			.ThrowsExactly<InvalidOperationException>();

		// Assert
		await Assert.That(exception!.Message).Contains(nameof(ItemNotFound));
	}

	[Test]
	public async Task Map_GivenUninitializedResult_ReturnsAnInternalServerErrorProblem()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(default(Result<int, HttpTestError>), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
		await Assert.That(response.Body).Contains("results_uninitialized");
	}

	[Test]
	public async Task ToHttpResult_GivenRegisteredMapper_UsesIt()
	{
		// Arrange
		await using var services = ResultsHttpTestFactory.CreateServices(options =>
			options.Map<ItemNotFound>(_ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound))
		);
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			new ItemNotFound(7).AsFailure<int>().ToHttpResult(context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	[Test]
	public async Task ToHttpResult_GivenExplicitMapper_UsesIt()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.Map<ItemNotFound>(_ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound))
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			new ItemNotFound(7).AsFailure<int>().ToHttpResult(mapper, context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	[Test]
	public async Task ToHttpResult_GivenSuccessfulResult_ReturnsOk()
	{
		// Arrange
		await using var services = ResultsHttpTestFactory.CreateServices();
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			Result<int, HttpTestError>.Success(42).ToHttpResult(context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
		await Assert.That(response.Body).Contains("42");
	}
}
