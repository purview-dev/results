using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

public sealed class DefaultResultsHttpMapperTests
{
	[Test]
	public async Task Map_GivenSuccessfulResult_ReturnsOkWithTheValue()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(Result<int, HttpTestError>.Success(42), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
		await Assert.That(response.Body).Contains("42");
	}

	[Test]
	public async Task Map_GivenSuccessfulValueThatIsAResponse_ReturnsItUnchanged()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(Result<IResult, HttpTestError>.Success(TypedResults.NoContent()), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status204NoContent);
	}

	[Test]
	public async Task Map_GivenMappedCase_ReturnsTheMappedResponse()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.Map<ItemNotFound>(notFound =>
				TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Item {notFound.ItemId}.")
			)
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
		await Assert.That(response.Body).Contains("Item 7.");
	}

	[Test]
	public async Task Map_GivenMapperForTheErrorType_MapsEveryCaseWithoutItsOwnMapping()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.Map<HttpTestError>(error =>
				error switch
				{
					ItemConflict conflict => TypedResults.Problem(
						statusCode: StatusCodes.Status409Conflict,
						title: conflict.Reason
					),
					_ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "rejected"),
				}
			)
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemConflict(7, "already exists").AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(response.Body).Contains("already exists");
	}

	[Test]
	public async Task Map_GivenCaseMappingAndErrorTypeMapping_PrefersTheCaseMapping()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
		{
			options.Map<HttpTestError>(_ => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict));
			options.Map<ItemNotFound>(_ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));
		});
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	[Test]
	public async Task Map_GivenFallbackForAnUnmappedCase_ReturnsTheFallbackResponse()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFallback((error, _) =>
				error is ItemRejected rejected
					? TypedResults.Problem(
						statusCode: StatusCodes.Status422UnprocessableEntity,
						title: $"rejected {rejected.ItemId}"
					)
					: null
			)
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemRejected(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(response.Body).Contains("rejected 7");
	}

	[Test]
	public async Task Map_GivenMappingAndUnusedFallback_ReturnsTheMappedResponse()
	{
		// Arrange
		var fallbackInvocations = 0;
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
		{
			options.Map<ItemNotFound>(_ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));
			options.AddFallback((_, _) =>
			{
				fallbackInvocations++;

				return null;
			});
		});
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
		await Assert.That(fallbackInvocations).IsEqualTo(0);
	}
}
