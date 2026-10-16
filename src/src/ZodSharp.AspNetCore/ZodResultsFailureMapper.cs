using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Purview.Results.AspNetCore;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

/// <summary>
/// Renders a result failure that carries ZodSharp validation errors, honouring the code, category and origin
/// rules registered on <see cref="ZodResultsHttpOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The failure's errors are matched against the registered rules — codes first, then categories, then origins,
/// each in registration order — and the first rule that answers wins. A rule with a status code renders the
/// standard validation problem with that default status; a rule with a factory renders whatever the factory
/// returns, and a factory returning <see langword="null"/> declines the failure so matching continues. When no
/// rule answers, the failure is rendered as the standard validation problem, so registering rules only ever
/// narrows what a host already gets.
/// </para>
/// <para>
/// A failure that does not carry validation errors is declined (<see langword="null"/>), so other mappers and
/// fallbacks still apply. Everything is rendered by <see cref="ZodValidationProblems.ToProblem"/>, the same
/// mapping a thrown <c>ZodException</c> uses, so a rule's response and the exception handler's response agree on
/// the status code, title, detail and <c>issues</c> extension.
/// </para>
/// </remarks>
public sealed partial class ZodResultsFailureMapper(
	IOptions<ZodProblemDetailsOptions> problemOptions,
	IOptions<ZodResultsHttpOptions> rules,
	IOptions<ResultsHttpOptions> results,
	ILogger<ZodResultsFailureMapper> logger
) : IResultsFailureMapper
{
	readonly ZodProblemDetailsOptions _problemOptions =
		problemOptions?.Value ?? throw new ArgumentNullException(nameof(problemOptions));
	readonly ZodResultsHttpOptions _rules = rules?.Value ?? throw new ArgumentNullException(nameof(rules));
	readonly ResultsHttpOptions _results = results?.Value ?? throw new ArgumentNullException(nameof(results));

	/// <inheritdoc />
	public IResult? Map(ResultsFailureContext context)
	{
		if (context.Case is not IValidationErrorCarrier carrier)
			return null;

		var errors = carrier.ValidationErrors;

		foreach (var rule in _rules.Candidates(errors))
		{
			// A factory declines a failure by returning null, and matching continues with the next rule.
			if (rule.Behaviour.Map is { } map)
			{
				if (map(errors, context.HttpContext) is { } mapped)
					return mapped;

				continue;
			}

			return Render(errors, context, rule.Behaviour.StatusCode ?? StatusCodes.Status400BadRequest, rule);
		}

		return Render(errors, context, StatusCodes.Status400BadRequest, rule: null);
	}

	IResult Render(
		ImmutableArray<ValidationError> errors,
		ResultsFailureContext context,
		int statusCode,
		ZodErrorRule? rule
	)
	{
		if (rule is not null && errors.Length > 1)
			Log.RuleAnsweredMultipleErrors(logger, rule.Value, errors.Length, statusCode);

		return ZodValidationProblems.ToProblem(
			errors,
			_problemOptions,
			statusCode,
			_results.IncludeTraceId ? context.HttpContext.TraceIdentifier : null
		);
	}

	/// <summary>
	/// The log messages this mapper emits, with stable <see cref="EventId"/>s.
	/// </summary>
	/// <remarks>
	/// Source-generated, so the identifier is fixed and the message allocates nothing when Debug is
	/// disabled — which also removes the hand-written <c>IsEnabled</c> guard the call site used to need.
	/// </remarks>
	static partial class Log
	{
		[LoggerMessage(
			EventId = 2000,
			Level = LogLevel.Debug,
			Message = "The validation error rule for '{Rule}' answered a failure carrying {ErrorCount} validation errors with status {StatusCode}."
		)]
		internal static partial void RuleAnsweredMultipleErrors(
			ILogger logger,
			string rule,
			int errorCount,
			int statusCode
		);
	}
}
