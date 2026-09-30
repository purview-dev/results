using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

			return Problem("The endpoint returned an uninitialized result.", "results_uninitialized");
		}

		return result.IsSuccess ? Success(result, context) : Failure(result, context);
	}

	IResult Success(IResultValue result, HttpContext context)
	{
		if (_options.SuccessMapper is { } successMapper)
			return successMapper(result.SuccessValue, context);

		// A result that already carries a response passes it through untouched.
		if (result.SuccessValue is IResult produced)
			return produced;

		// A result that carries a value is serialized with the configured status code.
		return _options.SuccessStatusCode == StatusCodes.Status200OK
			? TypedResults.Ok(result.SuccessValue)
			: TypedResults.Json(result.SuccessValue, statusCode: _options.SuccessStatusCode);
	}

	IResult Failure(IResultValue result, HttpContext context)
	{
		var error = result.ErrorValue;

		// The case of a union error, or the error itself when it is not a union.
		var caseValue = ResolveCaseValue(error);
		var caseType = caseValue?.GetType();
		var errorType = error?.GetType();

		if (caseType is not null && _options.TryGetMapper(caseType, out var caseMapper))
			return caseMapper(caseValue!, context);

		// A mapping registered for the error type itself handles every case without its own mapping.
		if (errorType is not null && errorType != caseType && _options.TryGetMapper(errorType, out var errorMapper))
			return errorMapper(error!, context);

		// Fallbacks see the case, so per-error-type behaviour does not have to unwrap a union itself.
		foreach (var fallback in _options.Fallbacks)
			if (fallback(caseValue, context) is { } mapped)
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

		return Problem(_options.UnmappedTitle, unmappedType?.FullName ?? "unknown");
	}

	ProblemHttpResult Problem(string title, string errorType)
	{
		ProblemDetails problem = new() { Status = _options.UnmappedStatusCode, Title = title };

		problem.Extensions["errorType"] = errorType;

		// ASP.NET Core's problem details writer adds the request's trace identifier itself, so it is not added here.
		return TypedResults.Problem(problem);
	}

	static object? ResolveCaseValue(object? error)
	{
		if (error is IUnion { Value: { } caseValue })
			return caseValue;

		// A non-union error is its own case.
		return error;
	}
}
