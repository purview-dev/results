using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

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

		// ZodSharp writes every ProblemDetails - its exception handler, endpoint filter and typed results - through
		// one ZodProblemDetailsWriter configured by ZodProblemDetailsOptions, so the same writer is used here rather
		// than duplicating the mapping rules. Handing it the whole options object (instead of only the registry, as
		// Purview.ZodSharp 2 required) keeps every option the host set - path naming, message formatting, issues,
		// parameter merging and the status-code precedence: default, then the highest ErrorType.HttpStatus, then
		// StatusCodeSelector, then StatusCodeOverride - identical to a thrown ZodException's response. The only
		// difference is the default status, which a ZodResultsHttpOptions rule may supply.
		ZodProblemDetailsWriter writer = new(WithDefaultStatusCode(options, defaultStatusCode));
		var problem = writer.Create(errors.IsDefault ? [] : errors.AsSpan());

		if (!string.IsNullOrEmpty(traceId))
			problem.Extensions["traceId"] = traceId;

		return WriteProblem(problem, defaultStatusCode);
	}

	/// <summary>
	/// The options with <paramref name="defaultStatusCode"/> as their default status, copied rather than mutated
	/// because the instance is the host's shared, configured options.
	/// </summary>
	/// <remarks>
	/// Every settable member of <see cref="ZodProblemDetailsOptions"/> is copied, which
	/// <c>ZodValidationProblemsTests.WithDefaultStatusCode_GivenADifferentStatus_CopiesEverySettableOption</c> pins,
	/// so an option a later ZodSharp version adds cannot be dropped silently.
	/// </remarks>
	/// <param name="options">The host's options.</param>
	/// <param name="defaultStatusCode">The default status to apply.</param>
	/// <returns><paramref name="options"/> itself when its default already matches; otherwise a copy.</returns>
	internal static ZodProblemDetailsOptions WithDefaultStatusCode(
		ZodProblemDetailsOptions options,
		int defaultStatusCode
	) =>
		options.DefaultStatusCode == defaultStatusCode
			? options
			: new()
			{
				Registry = options.Registry,
				ErrorTypeResolver = options.ErrorTypeResolver,
				FormatMessages = options.FormatMessages,
				PathNaming = options.PathNaming,
				IncludeIssues = options.IncludeIssues,
				MergeParametersIntoExtensions = options.MergeParametersIntoExtensions,
				ProjectDataAnnotationsIssues = options.ProjectDataAnnotationsIssues,
				DefaultStatusCode = defaultStatusCode,
				StatusCodeSelector = options.StatusCodeSelector,
				StatusCodeOverride = options.StatusCodeOverride,
			};

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "The serialized type is the concrete HttpValidationProblemDetails, not object, and it "
			+ "is the same type ASP.NET Core's own validation-problem results write, so a trimmed or AOT host "
			+ "already roots it. TypedResults.Json carries the requirement unconditionally regardless of T."
	)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:RequiresDynamicCode",
		Justification = "The serialized type is the concrete HttpValidationProblemDetails, not object, and it "
			+ "is the same type ASP.NET Core's own validation-problem results write, so a trimmed or AOT host "
			+ "already roots it. TypedResults.Json carries the requirement unconditionally regardless of T."
	)]
	// The concrete return type avoids boxing the result into IResult at every call site.
	static JsonHttpResult<HttpValidationProblemDetails> WriteProblem(
		HttpValidationProblemDetails problem,
		int defaultStatusCode
	)
	{
		// Written as JSON rather than through TypedResults.Problem because that serializes the declared
		// ProblemDetails shape and would drop the validation `errors` member this type carries.
		return TypedResults.Json(
			problem,
			contentType: "application/problem+json",
			statusCode: problem.Status ?? defaultStatusCode
		);
	}
}
