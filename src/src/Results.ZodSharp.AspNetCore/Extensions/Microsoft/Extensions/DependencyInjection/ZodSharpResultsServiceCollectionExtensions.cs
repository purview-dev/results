using Microsoft.Extensions.Options;
using Purview.Results.AspNetCore;
using Purview.Results.ZodSharp;
using Purview.Results.ZodSharp.AspNetCore;
using global::ZodSharp.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers ZodSharp validation mapping for results.
/// </summary>
public static class ZodSharpResultsServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Makes a result failure that carries ZodSharp validation errors produce an
		/// <c>HttpValidationProblemDetails</c> response.
		/// </summary>
		/// <remarks>
		/// The mapping is registered as a fallback, so a mapping the host registered for a specific error case always
		/// wins. Pair it with <c>AddZodSharpProblemDetails()</c> (and <c>AddResultsHttp()</c>) so results and thrown
		/// exceptions produce identical responses.
		/// </remarks>
		/// <returns>The service collection for chaining.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
		public IServiceCollection AddResultsZodSharpHttp()
		{
			ArgumentNullException.ThrowIfNull(services);

			// Registering the options keeps the mapping usable with defaults when a host maps results without the
			// ZodSharp exception handler; it is a no-op when that handler already registered them.
			services.AddOptions<ZodProblemDetailsOptions>();

			services.Configure<ResultsHttpOptions>(options =>
				options.AddFallback((error, context) =>
					error is IValidationErrorCarrier carrier
						? ZodValidationProblems.ToProblem(
							carrier.ValidationErrors,
							context.RequestServices.GetRequiredService<IOptions<ZodProblemDetailsOptions>>().Value,
							traceId: options.IncludeTraceId ? context.TraceIdentifier : null
						)
						: null
				)
			);

			return services;
		}
	}
}
