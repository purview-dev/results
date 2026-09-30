using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Tests for the failure-mapper extension point: a mapper registered with
/// <see cref="ResultsHttpOptions.AddFailureMapper{TMapper}"/> answers a failure by its shape and shares the one
/// ordered fallback stage with <see cref="ResultsHttpOptions.AddFallback"/>.
/// </summary>
public sealed class ResultsFailureMapperTests
{
	[Test]
	public async Task Map_GivenFailureMapper_ReturnsTheMappedResponse()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFailureMapper<ItemIdentifierFailureMapper>()
		);
		using var services = CreateServices<ItemIdentifierFailureMapper>();
		var context = ResultsHttpTestFactory.CreateContext(services);

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
	public async Task Map_GivenFailureMapperThatDefers_FallsThroughToTheNextFallback()
	{
		// Arrange
		// The mapper declines a conflict, so the fallback registered after it answers instead.
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options
				.AddFailureMapper<ItemIdentifierFailureMapper>()
				.AddFallback((error, _) => error is ItemConflict ? TypedResults.Conflict() : null)
		);
		using var services = CreateServices<ItemIdentifierFailureMapper>();
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemConflict(7, "taken").AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	[Test]
	public async Task Map_GivenCaseMappingAndFailureMapper_PrefersTheCaseMapping()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options
				.Map<ItemRejected>(_ => TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed))
				.AddFailureMapper<ItemIdentifierFailureMapper>()
		);
		using var services = CreateServices<ItemIdentifierFailureMapper>();
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemRejected(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status412PreconditionFailed);
	}

	[Test]
	public async Task Map_GivenUnionError_HandsTheMapperTheCaseAndTheError()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFailureMapper<ShapeRecordingFailureMapper>()
		);
		using var services = CreateServices<ShapeRecordingFailureMapper>();
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		await ResultsHttpTestFactory.ExecuteAsync(mapper.Map(new ItemNotFound(7).AsFailure<int>(), context), context);

		// Assert
		var observed = services.GetRequiredService<ShapeRecordingFailureMapper>().Observed;
		await Assert.That(observed).IsNotNull();
		await Assert.That(observed!.Value.Case).IsTypeOf<ItemNotFound>();
		await Assert.That(observed.Value.Error).IsTypeOf<HttpTestError>();
	}

	[Test]
	public async Task Map_GivenFailureMapperWithDependencies_ResolvesThemFromRequestServices()
	{
		// Arrange
		// The mapper takes its status code from a service, which is what registering a class instead of a
		// fallback delegate buys a host.
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFailureMapper<ConfiguredFailureMapper>()
		);
		using var services = CreateServices<ConfiguredFailureMapper>(collection =>
			collection.AddSingleton(new FailureMapperSettings(StatusCodes.Status410Gone))
		);
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemRejected(7).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status410Gone);
	}

	[Test]
	public async Task Map_GivenUnregisteredFailureMapper_ThrowsWithTheRegistrationToAdd()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFailureMapper<ItemIdentifierFailureMapper>()
		);
		var context = ResultsHttpTestFactory.CreateContext(new ServiceCollection().BuildServiceProvider());

		// Act
		InvalidOperationException? exception = null;
		try
		{
			mapper.Map(new ItemRejected(7).AsFailure<int>(), context);
		}
		catch (InvalidOperationException caught)
		{
			exception = caught;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains(nameof(ItemIdentifierFailureMapper));
		await Assert.That(exception.Message).Contains("AddSingleton");
	}

	[Test]
	public async Task Map_GivenSuccessfulResult_DoesNotConsultFailureMappers()
	{
		// Arrange
		var mapper = ResultsHttpTestFactory.CreateMapper(options =>
			options.AddFailureMapper<ShapeRecordingFailureMapper>()
		);
		using var services = CreateServices<ShapeRecordingFailureMapper>();
		var context = ResultsHttpTestFactory.CreateContext(services);

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(Result<int, HttpTestError>.Success(42), context),
			context
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
		await Assert.That(services.GetRequiredService<ShapeRecordingFailureMapper>().Observed).IsNull();
	}

	/// <summary>
	/// Builds a provider that has the mapper registered, which is what the options' mapper resolution expects.
	/// </summary>
	static ServiceProvider CreateServices<TMapper>(Action<IServiceCollection>? configure = null)
		where TMapper : class
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddSingleton<TMapper>();
		configure?.Invoke(services);

		return services.BuildServiceProvider();
	}
}

/// <summary>Records the context a mapper was handed, so the shape of the failure can be asserted on.</summary>
public sealed class ShapeRecordingFailureMapper : IResultsFailureMapper
{
	/// <summary>Gets the context of the last failure the mapper saw.</summary>
	public ResultsFailureContext? Observed { get; private set; }

	/// <inheritdoc />
	public IResult? Map(ResultsFailureContext context)
	{
		Observed = context;

		return null;
	}
}

/// <summary>Turns a failure whose payload is an item identifier into <c>422</c>.</summary>
public sealed class ItemIdentifierFailureMapper : IResultsFailureMapper
{
	/// <inheritdoc />
	public IResult? Map(ResultsFailureContext context) =>
		context.Case switch
		{
			ItemRejected rejected => TypedResults.Problem(
				statusCode: StatusCodes.Status422UnprocessableEntity,
				title: $"rejected {rejected.ItemId}"
			),
			_ => null,
		};
}

/// <summary>Answers with a configured status code, which is why it has a dependency at all.</summary>
public sealed class ConfiguredFailureMapper(FailureMapperSettings settings) : IResultsFailureMapper
{
	/// <inheritdoc />
	public IResult? Map(ResultsFailureContext context) =>
		context.Case is null ? null : TypedResults.StatusCode(settings.StatusCode);
}

/// <summary>The setting <see cref="ConfiguredFailureMapper"/> reads.</summary>
/// <param name="StatusCode">The status code the mapper answers with.</param>
public sealed record FailureMapperSettings(int StatusCode);
