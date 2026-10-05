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
/// A successful result is serialized with <see cref="ResultsHttpOptions.SuccessStatusCode"/>, a successful unit
/// result answers <c>204 No Content</c> because it carries no payload, a failed result is mapped by the
/// registration matching its most specific case, and a failure with no registration produces a
/// <see cref="ProblemDetails"/> with <see cref="ResultsHttpOptions.UnmappedStatusCode"/>. For a union error the
/// most specific case is the innermost active case, so a nested union resolves to its leaf.
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

		// A unit result carries no payload, so it answers 204 No Content rather than serializing the marker.
		if (result.SuccessValue is Success)
			return TypedResults.NoContent();

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

		// The failure's cases from the outermost error inward to the leaf: a non-union error is its own case, a
		// union contributes its active case, and a nested union case contributes its own active case, so a host
		// can map the leaf a nested union ultimately carries.
		var cases = ResolveCases(error);
		var caseValue = cases[^1];
		var caseType = caseValue?.GetType();

		// The most specific case wins: a mapping for the leaf beats one for an enclosing union, which beats one
		// for the error type. A mapping registered for the error type therefore covers every case without its own.
		for (var index = cases.Count - 1; index >= 0; index--)
		{
			if (cases[index] is { } candidate && _options.TryGetMapper(candidate.GetType(), out var mapper))
				return mapper(candidate, context);
		}

		// Fallbacks see the leaf case, so per-error-type behaviour does not have to unwrap a union itself; the
		// context also carries the error, so a shape-based mapper can look at the union as a whole.
		ResultsFailureContext failure = new(caseValue, error, context);

		foreach (var fallback in _options.Fallbacks)
			if (fallback(failure) is { } mapped)
				return mapped;

		var unmappedType = caseType ?? error?.GetType();

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

	static List<object?> ResolveCases(object? error)
	{
		if (error is not IUnion { Value: { } caseValue })
			return [error];

		List<object?> cases = [error];

		while (caseValue is IUnion { Value: { } nestedCase })
		{
			cases.Add(caseValue);
			caseValue = nestedCase;
		}

		cases.Add(caseValue);

		return cases;
	}
}
