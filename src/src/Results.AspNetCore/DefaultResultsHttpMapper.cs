using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Purview.Results.AspNetCore;

/// <summary>
/// The default <see cref="IResultsHttpMapper"/>.
/// </summary>
/// <remarks>
/// A successful result is serialized with <see cref="ResultsHttpOptions.SuccessStatusCode"/>, a failed result is
/// mapped by the registration matching its error case, and a failure with no registration produces a
/// <see cref="ProblemDetails"/> with <see cref="ResultsHttpOptions.UnmappedStatusCode"/>.
/// </remarks>
public sealed class DefaultResultsHttpMapper(
	IOptions<ResultsHttpOptions> options,
	ILogger<DefaultResultsHttpMapper> logger
) : IResultsHttpMapper
{
	readonly ResultsHttpOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="context"/> is null.
	/// </exception>
	public IResult Map(IResultValue result, HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(context);

		if (!result.IsInitialized)
		{
			logger.LogError(
				"The endpoint returned an uninitialized result; returning {StatusCode}.",
				_options.UnmappedStatusCode
			);

			return Problem(
				context,
				"The endpoint returned an uninitialized result.",
				"results_uninitialized"
			);
		}

		if (result.IsSuccess)
			return Success(result, context);

		return Failure(result, context);
	}

	IResult Success(IResultValue result, HttpContext context)
	{
		if (_options.SuccessMapper is { } successMapper)
			return successMapper(result.Value, context);

		// A result that already carries a response passes it through untouched.
		if (result.Value is IResult produced)
			return produced;

		return _options.SuccessStatusCode == StatusCodes.Status200OK
			? TypedResults.Ok(result.Value)
			: TypedResults.Json(result.Value, statusCode: _options.SuccessStatusCode);
	}

	IResult Failure(IResultValue result, HttpContext context)
	{
		var error = result.Error;
		var errorType = error?.GetType();

		// The case type of a union error, or the error type itself when the error is not a union.
		var caseType = ResolveCaseType(error);

		if (caseType is not null && _options.TryGetMapper(caseType, out var caseMapper))
			return caseMapper(error!, context);

		// A mapping registered for the error type itself handles every case without its own mapping.
		if (errorType is not null && _options.TryGetMapper(errorType, out var errorMapper))
			return errorMapper(error!, context);

		foreach (var fallback in _options.Fallbacks)
			if (fallback(error, context) is { } mapped)
				return mapped;

		var unmappedType = caseType ?? errorType;

		if (_options.ThrowOnUnmappedFailure)
		{
			throw new InvalidOperationException(
				$"No HTTP mapping is registered for the error case '{unmappedType?.FullName ?? "<unknown>"}'."
			);
		}

		logger.LogError(
			"No HTTP mapping is registered for the error case {ErrorCase}; returning {StatusCode}.",
			unmappedType?.FullName ?? "<unknown>",
			_options.UnmappedStatusCode
		);

		return Problem(context, _options.UnmappedTitle, unmappedType?.FullName ?? "unknown");
	}

	IResult Problem(HttpContext context, string title, string errorType)
	{
		ProblemDetails problem = new()
		{
			Status = _options.UnmappedStatusCode,
			Title = title,
		};

		problem.Extensions["errorType"] = errorType;

		if (_options.IncludeTraceId)
			problem.Extensions["traceId"] = context.TraceIdentifier;

		return TypedResults.Problem(problem);
	}

	static Type? ResolveCaseType(object? error)
	{
		if (error is IUnion { Value: { } caseValue })
			return caseValue.GetType();

		return error?.GetType();
	}
}
