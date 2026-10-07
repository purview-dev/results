using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#if NET11_0_OR_GREATER
// Only the union-unwrapping path, which is compiled for net11.0 only, needs this.
using System.Runtime.CompilerServices;
#endif

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
public sealed partial class DefaultResultsHttpMapper(
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
			ResultsHttpTelemetry.Record(ResultsHttpTelemetry.ResultsUninitialized, errorType: null);
			Log.UninitializedResult(logger, _options.UnmappedStatusCode);

			return Problem(
				"The endpoint returned an uninitialized result.",
				"results_uninitialized",
				isClrTypeName: false
			);
		}

		return result.IsSuccess ? Success(result, context) : Failure(result, context);
	}

	IResult Success(IResultValue result, HttpContext context)
	{
		if (_options.SuccessMapper is { } successMapper)
			return successMapper(result.SuccessValue, context);

		// Dispatch through the result so the value reaches TypedResults with its static type rather than as
		// object. Binding Ok<object> leaves a trimmed or AOT host with no JsonTypeInfo to resolve, and it also
		// erases the response type ASP.NET Core would otherwise surface to OpenAPI.
		return result.AcceptSuccess(SuccessWriter.Instance, (context, _options.SuccessStatusCode));
	}

	/// <summary>
	/// Writes a successful value with its static type intact. See <see cref="IResultValueVisitor{TState, TReturn}"/>.
	/// </summary>
	sealed class SuccessWriter : IResultValueVisitor<(HttpContext Context, int StatusCode), IResult>
	{
		internal static readonly SuccessWriter Instance = new();

		public IResult VisitSuccess<TValue>(TValue value, (HttpContext Context, int StatusCode) state)
		{
			// A unit result carries no payload, so it answers 204 No Content rather than serializing the marker.
			if (value is Success)
				return TypedResults.NoContent();

			// A result that already carries a response passes it through untouched.
			if (value is IResult produced)
				return produced;

			// TValue is the declared value type here, so Ok<TValue> is both AOT-resolvable and the correct
			// response type for OpenAPI inference.
			return state.StatusCode == StatusCodes.Status200OK ? TypedResults.Ok(value) : Json(value, state.StatusCode);
		}

		[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:RequiresUnreferencedCode",
			Justification = "TValue is the endpoint's declared result value type, not object, so a trimmed or "
				+ "AOT host resolves it through its own JsonSerializerContext. Only a non-200 SuccessStatusCode "
				+ "reaches here; the default path uses TypedResults.Ok, which carries no such requirement."
		)]
		[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "TValue is the endpoint's declared result value type, not object, so a trimmed or "
				+ "AOT host resolves it through its own JsonSerializerContext. Only a non-200 SuccessStatusCode "
				+ "reaches here; the default path uses TypedResults.Ok, which carries no such requirement."
		)]
		// The concrete return type avoids boxing the result into IResult at every call site.
		static JsonHttpResult<TValue> Json<TValue>(TValue value, int statusCode) =>
			TypedResults.Json(value, statusCode: statusCode);
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
			{
				ResultsHttpTelemetry.Record(ResultsHttpTelemetry.FailuresMapped, caseType?.FullName);

				return mapper(candidate, context);
			}
		}

		// Fallbacks see the leaf case, so per-error-type behaviour does not have to unwrap a union itself; the
		// context also carries the error, so a shape-based mapper can look at the union as a whole.
		ResultsFailureContext failure = new(caseValue, error, context);

		foreach (var fallback in _options.Fallbacks)
		{
			if (fallback(failure) is { } mapped)
			{
				ResultsHttpTelemetry.Record(ResultsHttpTelemetry.FailuresMapped, caseType?.FullName);

				return mapped;
			}
		}

		var unmappedType = caseType ?? error?.GetType();

		if (_options.ThrowOnUnmappedFailure)
		{
			throw new InvalidOperationException(
				$"No HTTP mapping is registered for the error case '{unmappedType?.FullName ?? "<unknown>"}'."
			);
		}

		// Counted and traced regardless of whether the type name is disclosed in the response: metrics and
		// traces stay inside your infrastructure, so this is how a mapping gap stays diagnosable.
		ResultsHttpTelemetry.Record(ResultsHttpTelemetry.FailuresUnmapped, unmappedType?.FullName);
		Log.UnmappedFailure(logger, unmappedType?.FullName ?? "<unknown>", _options.UnmappedStatusCode);

		return Problem(_options.UnmappedTitle, unmappedType?.FullName ?? "unknown", isClrTypeName: true);
	}

	/// <param name="title">The problem title.</param>
	/// <param name="errorType">The value for the <c>errorType</c> extension.</param>
	/// <param name="isClrTypeName">
	/// Whether <paramref name="errorType"/> is a CLR type name rather than a fixed discriminator. A type
	/// name discloses internal namespace topology and domain vocabulary to the caller, so it is written
	/// only when the host opted in; it is logged either way, so a mapping gap stays diagnosable from your
	/// own logs. A fixed discriminator such as <c>results_uninitialized</c> carries no such information and
	/// is always written.
	/// </param>
	ProblemHttpResult Problem(string title, string errorType, bool isClrTypeName)
	{
		ProblemDetails problem = new() { Status = _options.UnmappedStatusCode, Title = title };

		if (!isClrTypeName || _options.IncludeErrorTypeInProblemDetails)
			problem.Extensions["errorType"] = errorType;

		// ASP.NET Core's problem details writer adds the request's trace identifier itself, so it is not added here.
		return TypedResults.Problem(problem);
	}

	/// <summary>
	/// The failure's cases, outermost error first and the active leaf last.
	/// </summary>
	/// <remarks>
	/// Union unwrapping is compiled only for <c>net11.0</c>. Unions are a .NET 11 feature — the compiler
	/// needs <c>System.Runtime.CompilerServices.IUnion</c> and <c>UnionAttribute</c>, neither of which exists
	/// earlier — so a <c>net10.0</c> consumer cannot declare one and therefore cannot produce a union error.
	/// On that target the error is its own single case, which is the same answer the union path gives for a
	/// non-union error.
	/// </remarks>
	static List<object?> ResolveCases(object? error)
	{
#if NET11_0_OR_GREATER
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
#else
		return [error];
#endif
	}

	/// <summary>
	/// The log messages this mapper emits, with stable <see cref="EventId"/>s.
	/// </summary>
	/// <remarks>
	/// Source-generated so the identifiers are fixed and the messages allocate nothing when the level is
	/// disabled. The statements previously carried no <c>EventId</c>, which left alerting on "an unmapped
	/// failure happened" to string-matching the message template.
	/// </remarks>
	static partial class Log
	{
		[LoggerMessage(
			EventId = 1000,
			Level = LogLevel.Error,
			Message = "The endpoint returned an uninitialized result; returning {StatusCode}."
		)]
		internal static partial void UninitializedResult(ILogger logger, int statusCode);

		[LoggerMessage(
			EventId = 1001,
			Level = LogLevel.Error,
			Message = "No HTTP mapping is registered for the error case {ErrorCase}; returning {StatusCode}."
		)]
		internal static partial void UnmappedFailure(ILogger logger, string errorCase, int statusCode);
	}
}
