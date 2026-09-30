using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Purview.Results.AspNetCore;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp;

/// <summary>
/// Converts the validation errors a result failure carries into an HTTP validation problem.
/// </summary>
public static class ValidationProblemResultExtensions
{
	extension(IValidationErrorCarrier carrier)
	{
		/// <summary>
		/// Creates the validation problem response for the errors the error value carries.
		/// </summary>
		/// <param name="context">The current request.</param>
		/// <param name="statusCode">
		/// The status code used when a resolved error type does not define one. Defaults to <c>400 Bad Request</c>.
		/// </param>
		/// <returns>The validation problem response.</returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="carrier"/> or <paramref name="context"/> is null.
		/// </exception>
		public IResult ToValidationProblem(HttpContext context, int statusCode = StatusCodes.Status400BadRequest)
		{
			ArgumentNullException.ThrowIfNull(carrier);
			ArgumentNullException.ThrowIfNull(context);

			return carrier.ValidationErrors.ToValidationProblem(context, statusCode);
		}
	}

	extension(ImmutableArray<ValidationError> errors)
	{
		/// <summary>
		/// Creates the validation problem response for the errors.
		/// </summary>
		/// <param name="context">The current request.</param>
		/// <param name="statusCode">
		/// The status code used when a resolved error type does not define one. Defaults to <c>400 Bad Request</c>.
		/// </param>
		/// <returns>The validation problem response.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
		public IResult ToValidationProblem(HttpContext context, int statusCode = StatusCodes.Status400BadRequest)
		{
			ArgumentNullException.ThrowIfNull(context);

			var options =
				context.RequestServices?.GetService<IOptions<ZodProblemDetailsOptions>>()?.Value
				?? new ZodProblemDetailsOptions();

			var includeTraceId =
				context.RequestServices?.GetService<IOptions<ResultsHttpOptions>>()?.Value.IncludeTraceId ?? true;

			return ZodValidationProblems.ToProblem(
				errors,
				options,
				statusCode,
				includeTraceId ? context.TraceIdentifier : null
			);
		}
	}
}
