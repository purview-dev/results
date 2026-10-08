using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Purview.Results.AspNetCore;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

/// <summary>
/// Tests for <see cref="ZodResultsFailureMapper"/> and <see cref="ZodResultsHttpOptions"/>: the rules a host
/// registers per validation error code, category and origin, and how they sit in the failure resolution order.
/// </summary>
public sealed class ZodResultsFailureMapperTests
{
	const string InvalidNameCode = "invalid_name";
	const string MissingCode = "missing";
	const string InvalidValueCategory = "invalid_value";
	const string ValueObjectOrigin = "value_object";
	const string InvalidTenantIdCode = "invalid_tenant_id";
	const string NullableAliasInvalidCode = "invalid_string";

	[Test]
	public async Task Map_GivenCodeRuleWithStatus_ReturnsTheValidationProblemWithThatStatus()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenCodeRuleFactory_ReturnsThatFactoryResponse()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, (_, _) => TypedResults.NotFound())
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	[Test]
	public async Task Map_GivenCategoryRule_ReturnsTheValidationProblemWithThatStatus()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapCategory(InvalidValueCategory, StatusCodes.Status409Conflict)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(7, CreateCategorisedErrors(InvalidNameCode, InvalidValueCategory)).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenCodeAndCategoryRulesForTheSameFailure_PrefersTheCodeRule()
	{
		// Arrange
		// A code is narrower than a category, so it wins however the host registered the two.
		await using var services = CreateServices(zod =>
			zod.MapCategory(InvalidValueCategory, StatusCodes.Status409Conflict)
				.MapCode(InvalidNameCode, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(7, CreateCategorisedErrors(InvalidNameCode, InvalidValueCategory)).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
	}

	[Test]
	public async Task Map_GivenOriginRule_ReturnsTheValidationProblemWithThatStatus()
	{
		// Arrange
		// An origin is the structured origin a rule owns, so one origin rule answers a whole family of codes
		// that a type-level rule reports as "value_object".
		await using var services = CreateServices(zod =>
			zod.MapOrigin(ValueObjectOrigin, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateOriginErrors(ValueObjectOrigin)).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenOriginRuleFactory_ReturnsThatFactoryResponse()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapOrigin(ValueObjectOrigin, (_, _) => TypedResults.Conflict())
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateOriginErrors(ValueObjectOrigin)).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	[Test]
	public async Task Map_GivenOriginAndCategoryRulesForTheSameFailure_PrefersTheCategoryRule()
	{
		// Arrange
		// An origin is the broadest of the three axes, so it is only consulted after the code and category
		// rules, however the host registered the rules.
		await using var services = CreateServices(zod =>
			zod.MapOrigin(ValueObjectOrigin, StatusCodes.Status400BadRequest)
				.MapCategory(InvalidValueCategory, StatusCodes.Status409Conflict)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(
					7,
					CreateErrorsWithCategoryAndOrigin(InvalidValueCategory, ValueObjectOrigin)
				).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	[Test]
	public async Task Map_GivenCodeAndOriginRulesForTheSameFailure_PrefersTheCodeRule()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapOrigin(ValueObjectOrigin, StatusCodes.Status409Conflict)
				.MapCode(InvalidNameCode, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateOriginErrors(ValueObjectOrigin)).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
	}

	[Test]
	public async Task Map_GivenDecliningCodeRuleAndMatchingOriginRule_FallsThroughToTheOriginRule()
	{
		// Arrange
		// The strict code rule declines a mixed failure, so the origin rule registered for the family of rules
		// answers it instead of the failure losing its validation response.
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, (errors, _) => errors.Length == 1 ? TypedResults.NotFound() : null)
				.MapOrigin(ValueObjectOrigin, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(
					7,
					CreateOriginErrors(ValueObjectOrigin, InvalidNameCode, MissingCode)
				).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains(MissingCode);
	}

	[Test]
	public async Task Map_GivenAFailureFromAValueObjectRule_AnswersByOrigin()
	{
		// Arrange
		// The origin is the axis a rule owns, so an origin rule answers every type-level rule failure without
		// naming a code. The code and origin come from the real generated schema, not a hand-built error, so this
		// proves the identity a value-object rule reports reaches the HTTP mapping end to end.
		await using var services = CreateServices(zod =>
			zod.MapOrigin(ValueObjectOrigin, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		var validation = ValidatedTenantIdSchema.Validate(ValidatedTenantId.Hydrate(Guid.Empty));
		await Assert.That(validation.IsSuccess).IsFalse();

		// Act
		var result = validation.ToResult<ValidatedTenantId, ValidationTestError>(errors => new InputRejected(
			7,
			errors
		));
		var (statusCode, body) = await ExecuteAsync(mapper.Map(result, context), context);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains("invalid_tenant_id");
		await Assert.That(body).Contains(ValueObjectOrigin);
	}

	[Test]
	public async Task Map_GivenAFailureFromANullableScalarRule_AnswersByItsCode()
	{
		// Arrange
		// A nullable scalar round-trips null, so its type-level rule is the null-tolerant one: null validates and
		// whitespace-only is rejected. The code it reports is enough for a code rule to answer the failure.
		await using var services = CreateServices(zod =>
			zod.MapCode(NullableAliasInvalidCode, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		await Assert.That(NullableTenantAliasSchema.Validate(NullableTenantAlias.Hydrate(null)).IsSuccess).IsTrue();

		var validation = NullableTenantAliasSchema.Validate(NullableTenantAlias.Hydrate("   "));
		await Assert.That(validation.IsSuccess).IsFalse();

		// Act
		var result = validation.ToResult<NullableTenantAlias, ValidationTestError>(errors => new InputRejected(
			7,
			errors
		));
		var (statusCode, body) = await ExecuteAsync(mapper.Map(result, context), context);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains(NullableAliasInvalidCode);
	}

	[Test]
	public async Task Map_GivenAUnitResultFailureFromAValueObjectRule_AnswersTheValidationProblem()
	{
		// Arrange
		// A command that reports only why it was rejected folds the validation outcome into a unit result; the
		// value being discarded must not cost the failure its validation response.
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidTenantIdCode, StatusCodes.Status422UnprocessableEntity)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		var validation = ValidatedTenantIdSchema.Validate(ValidatedTenantId.Hydrate(Guid.Empty));

		// Act
		var result = validation.ToUnitResult<ValidatedTenantId, ValidationTestError>(errors => new InputRejected(
			7,
			errors
		));
		var (statusCode, body) = await ExecuteAsync(mapper.Map(result, context), context);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(body).Contains(InvalidTenantIdCode);
	}

	[Test]
	public async Task MapOrigin_GivenEmptyOrigin_Throws()
	{
		// Arrange
		ZodResultsHttpOptions options = new();

		// Act
		var exception = CaptureArgumentException(() =>
			options.MapOrigin(string.Empty, StatusCodes.Status400BadRequest)
		);

		// Assert
		await Assert.That(exception).IsNotNull();
	}

	[Test]
	public async Task MapOrigin_GivenTheSameOriginTwiceWithDifferentBehaviour_Throws()
	{
		// Arrange
		ZodResultsHttpOptions options = new();
		options.MapOrigin(ValueObjectOrigin, StatusCodes.Status400BadRequest);

		// Act
		var exception = CaptureArgumentException(() =>
			options.MapOrigin(ValueObjectOrigin, StatusCodes.Status409Conflict)
		);

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains(ValueObjectOrigin);
	}

	[Test]
	public async Task Map_GivenDecliningOriginRule_FallsThroughToTheDefaultValidationProblem()
	{
		// Arrange
		// The strict origin rule declines a mixed failure, so the failure keeps the default validation response.
		await using var services = CreateServices(zod =>
			zod.MapOrigin(
				ValueObjectOrigin,
				(errors, _) => errors.Length == 1 && errors[0].Code == InvalidNameCode ? TypedResults.NotFound() : null
			)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(
					7,
					CreateOriginErrors(ValueObjectOrigin, InvalidNameCode, MissingCode)
				).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(body).Contains(MissingCode);
	}

	[Test]
	public async Task Map_GivenACarrierWithNoValidationErrors_ReturnsTheDefaultValidationProblem()
	{
		// Arrange
		await using var services = CreateServices(zod => zod.MapCode(InvalidNameCode, StatusCodes.Status409Conflict));
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, []).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(body).DoesNotContain(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenNoMatchingRule_ReturnsTheDefaultValidationProblem()
	{
		// Arrange
		await using var services = CreateServices(zod => zod.MapCode("some_other_code", StatusCodes.Status409Conflict));
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenCodeRuleFactoryThatDeclines_FallsThroughToTheDefaultValidationProblem()
	{
		// Arrange
		// A factory can tighten the match rule: here it only answers a failure whose every error is the code.
		await using var services = CreateServices(zod =>
			zod.MapCode(
				InvalidNameCode,
				(errors, _) => errors.Length == 1 && errors[0].Code == InvalidNameCode ? TypedResults.NotFound() : null
			)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors(InvalidNameCode, MissingCode)).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(body).Contains(MissingCode);
	}

	[Test]
	public async Task Map_GivenDecliningCodeRuleAndMatchingCategoryRule_FallsThroughToTheCategoryRule()
	{
		// Arrange
		// The strict code rule declines a mixed failure, so the category rule registered for a broader group
		// answers it instead of the failure losing its validation response.
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, (errors, _) => errors.Length == 1 ? TypedResults.NotFound() : null)
				.MapCategory(InvalidValueCategory, StatusCodes.Status409Conflict)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(
				new InputRejected(
					7,
					CreateErrorsWithCategory(InvalidValueCategory, InvalidNameCode, MissingCode)
				).AsFailure<int>(),
				context
			),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(body).Contains(MissingCode);
	}

	[Test]
	public async Task Map_GivenMultipleErrorsAndAMatchingCodeRule_ListsEveryErrorInTheResponse()
	{
		// Arrange
		// A rule matches when any error carries the code, and the response still carries the whole set, so a
		// rule never hides the other problems the caller has to fix.
		await using var services = CreateServices(zod => zod.MapCode(MissingCode, StatusCodes.Status409Conflict));
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors(MissingCode, InvalidNameCode)).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(body).Contains(MissingCode);
		await Assert.That(body).Contains(InvalidNameCode);
	}

	[Test]
	public async Task Map_GivenCaseMappingAndCodeRule_PrefersTheCaseMapping()
	{
		// Arrange
		await using var services = CreateServices(
			zod => zod.MapCode(InvalidNameCode, StatusCodes.Status409Conflict),
			results => results.Map<InputRejected>(_ => TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed))
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status412PreconditionFailed);
	}

	[Test]
	public async Task Map_GivenHostFailureMapperRegisteredFirst_AnswersBeforeTheValidationMapping()
	{
		// Arrange
		// A host mapper registered in AddResultsHttp runs before the ZodSharp mapping, which is how a host that
		// does not want the code/category/origin API answers validation carriers itself.
		await using var services = CreateServices(
			results: results => results.AddFailureMapper<HostValidationFailureMapper>(),
			configureServices: collection => collection.AddSingleton<HostValidationFailureMapper>()
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status413PayloadTooLarge);
	}

	[Test]
	public async Task Map_GivenErrorThatIsNotAValidationCarrier_DeclinesAndReturnsTheUnmappedProblem()
	{
		// Arrange
		await using var services = CreateServices(zod => zod.MapCode(InvalidNameCode, StatusCodes.Status409Conflict));
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, body) = await ExecuteAsync(mapper.Map(new ItemMissing(7).AsFailure<int>(), context), context);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
		// Identified by the unmapped title rather than the error's CLR type name, which is opt-in.
		await Assert.That(body).Contains("is not mapped to an HTTP response");
		await Assert.That(body).DoesNotContain(nameof(ItemMissing));
	}

	[Test]
	public async Task MapCode_GivenEmptyCode_Throws()
	{
		// Arrange
		ZodResultsHttpOptions options = new();

		// Act
		var exception = CaptureArgumentException(() => options.MapCode(string.Empty, StatusCodes.Status400BadRequest));

		// Assert
		await Assert.That(exception).IsNotNull();
	}

	[Test]
	public async Task MapCode_GivenTheSameCodeTwiceWithDifferentBehaviour_Throws()
	{
		// Arrange
		// Two answers for one code are ambiguous, so the mistake is caught when the host configures it rather
		// than per request.
		ZodResultsHttpOptions options = new();
		options.MapCode(InvalidNameCode, StatusCodes.Status400BadRequest);

		// Act
		var exception = CaptureArgumentException(() => options.MapCode(InvalidNameCode, StatusCodes.Status409Conflict));

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains(InvalidNameCode);
	}

	[Test]
	public async Task MapCode_GivenTheSameFactoryTwice_KeepsTheSingleRule()
	{
		// Arrange
		// A method group allocates a new delegate on every use, so identity is not how "the same behaviour" is
		// recognised: registering the same factory again is the same rule, not a conflicting one.
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, NotAValidationError).MapCode(InvalidNameCode, NotAValidationError)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status404NotFound);
	}

	[Test]
	public async Task MapCode_GivenTheSameCodeTwiceWithTheSameBehaviour_KeepsTheSingleRule()
	{
		// Arrange
		await using var services = CreateServices(zod =>
			zod.MapCode(InvalidNameCode, StatusCodes.Status409Conflict)
				.MapCode(InvalidNameCode, StatusCodes.Status409Conflict)
		);
		var mapper = services.GetRequiredService<IResultsHttpMapper>();
		var context = CreateContext(services);

		// Act
		var (statusCode, _) = await ExecuteAsync(
			mapper.Map(new InputRejected(7, CreateErrors()).AsFailure<int>(), context),
			context
		);

		// Assert
		await Assert.That(statusCode).IsEqualTo(StatusCodes.Status409Conflict);
	}

	/// <summary>Builds the provider the tests resolve the mapper from.</summary>
	static ServiceProvider CreateServices(
		Action<ZodResultsHttpOptions>? zod = null,
		Action<ResultsHttpOptions>? results = null,
		Action<IServiceCollection>? configureServices = null
	)
	{
		ServiceCollection services = new();
		services.AddLogging();
		configureServices?.Invoke(services);
		services.AddResultsHttp(results);
		services.AddResultsZodSharpHttp(zod);

		return services.BuildServiceProvider();
	}

	/// <summary>Creates a failure's errors, defaulting to the invalid name code.</summary>
	static ImmutableArray<ValidationError> CreateErrors(params string[] codes) =>
		[
			.. (codes.Length > 0 ? codes : [InvalidNameCode]).Select(static code =>
				ValidationError.Create(code, $"The {code} check failed.", ["Name"])
			),
		];

	/// <summary>Creates a failure's errors with a category, which is what a category rule looks for.</summary>
	static ImmutableArray<ValidationError> CreateCategorisedErrors(string code, string category) =>
		[ValidationError.Create(code, $"The {code} check failed.", ["Name"], category: category)];

	/// <summary>Creates several errors that share one category.</summary>
	static ImmutableArray<ValidationError> CreateErrorsWithCategory(string category, params string[] codes) =>
		[
			.. codes.Select(code =>
				ValidationError.Create(code, $"The {code} check failed.", ["Name"], category: category)
			),
		];

	/// <summary>Creates a failure's errors with an origin, which is what an origin rule looks for.</summary>
	static ImmutableArray<ValidationError> CreateOriginErrors(string origin, params string[] codes) =>
		[
			.. (codes.Length > 0 ? codes : [InvalidNameCode]).Select(code =>
				ValidationError.Create(code, $"The {code} check failed.", ["Name"], origin: origin)
			),
		];

	/// <summary>Creates errors that carry both a category and an origin, so precedence can be exercised.</summary>
	static ImmutableArray<ValidationError> CreateErrorsWithCategoryAndOrigin(string category, string origin) =>
		[
			ValidationError.Create(
				InvalidNameCode,
				"The name check failed.",
				["Name"],
				origin: origin,
				category: category
			),
		];

	/// <summary>Runs an action that must reject the host's configuration.</summary>
	static ArgumentException? CaptureArgumentException(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (ArgumentException caught)
		{
			return caught;
		}
	}

	/// <summary>The factory the same-factory-twice test registers, named so it is one method group.</summary>
	static IResult NotAValidationError(ImmutableArray<ValidationError> errors, HttpContext context) =>
		TypedResults.NotFound();

	static DefaultHttpContext CreateContext(IServiceProvider services) =>
		new() { Response = { Body = new MemoryStream() }, RequestServices = services };

	static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result, HttpContext context)
	{
		await result.ExecuteAsync(context);

		context.Response.Body.Seek(0, SeekOrigin.Begin);
		using StreamReader reader = new(context.Response.Body);
		var body = await reader.ReadToEndAsync();

		return (context.Response.StatusCode, body);
	}
}

/// <summary>Answers any validation carrier itself, which is how a host overrides the ZodSharp mapping.</summary>
public sealed class HostValidationFailureMapper : IResultsFailureMapper
{
	/// <inheritdoc />
	public IResult? Map(ResultsFailureContext context) =>
		context.Case is IValidationErrorCarrier ? TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge) : null;
}
