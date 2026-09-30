using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using global::ZodSharp;
using global::ZodSharp.AspNetCore;
using global::ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

/// <summary>
/// Maps ZodSharp validation errors onto an ASP.NET Core validation problem response.
/// </summary>
public static class ZodValidationProblems
{
	/// <summary>
	/// Creates the response for a set of validation errors.
	/// </summary>
	/// <remarks>
	/// The status code, title, detail, messages and the structured <c>issues</c> extension are produced by the
	/// same mapping the ZodSharp exception handler uses, so a validation failure carried by a result and the same
	/// failure thrown as a <see cref="ZodException"/> produce identical responses.
	/// </remarks>
	/// <param name="errors">The validation errors to render.</param>
	/// <param name="options">The ZodSharp problem options that resolve error types and the status code.</param>
	/// <param name="defaultStatusCode">
	/// The status code used when a resolved <see cref="ErrorType"/> does not define one. Defaults to
	/// <c>400 Bad Request</c>.
	/// </param>
	/// <param name="traceId">The request's trace identifier, when it should be included.</param>
	/// <returns>The validation problem response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	public static IResult ToProblem(
		ImmutableArray<ValidationError> errors,
		ZodProblemDetailsOptions options,
		int defaultStatusCode = StatusCodes.Status400BadRequest,
		string? traceId = null
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		var statusCode = options.StatusCodeSelector?.Invoke(errors) ?? defaultStatusCode;

		// ZodSharp maps ProblemDetails through validation results, thrown exceptions and its exception handler, and
		// every one of them shares one mapper. The exception view is the public shape that accepts raw errors, so it
		// is reused here rather than duplicating the mapping rules.
		HttpValidationProblemDetails problem = new ZodException(errors).ToHttpValidationProblemDetails(
			options.Registry,
			statusCode
		);

		if (!string.IsNullOrEmpty(traceId))
			problem.Extensions["traceId"] = traceId;

		return TypedResults.ValidationProblem(
			errors: problem.Errors,
			detail: problem.Detail,
			statusCode: problem.Status,
			title: problem.Title,
			type: problem.Type,
			extensions: problem.Extensions
		);
	}
}
