using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Purview.Results.AspNetCore;
using Purview.Results.ZodSharp;
using ZodSharp.AspNetCore;

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
		/// <c>HttpValidationProblemDetails</c> response, with optional rules per validation error code and
		/// category.
		/// </summary>
		/// <param name="configure">
		/// Configures which responses particular validation error codes and categories produce. Without it, every
		/// failure that carries validation errors is rendered as one validation problem.
		/// </param>
		/// <remarks>
		/// <para>
		/// The mapping is registered as a failure mapper, so a mapping the host registered for a specific error case
		/// always wins, a mapping registered for the error type wins, and a failure mapper the host registered
		/// before this call wins too — that is how a host answers some validation codes itself.
		/// </para>
		/// <para>
		/// Pair it with <c>AddZodSharpProblemDetails()</c> (and <c>AddResultsHttp()</c>) so results and thrown
		/// exceptions produce identical responses.
		/// </para>
		/// </remarks>
		/// <returns>The service collection for chaining.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
		public IServiceCollection AddResultsZodSharpHttp(Action<ZodResultsHttpOptions>? configure = null)
		{
			ArgumentNullException.ThrowIfNull(services);

			// Registering the options keeps the mapping usable with defaults when a host maps results without the
			// ZodSharp exception handler; it is a no-op when that handler already registered them.
			services.AddOptions<ZodProblemDetailsOptions>();
			services.AddOptions<ZodResultsHttpOptions>();

			if (configure is not null)
				services.Configure(configure);

			services.TryAddSingleton<ZodResultsFailureMapper>();

			services.Configure<ResultsHttpOptions>(options => options.AddFailureMapper<ZodResultsFailureMapper>());

			return services;
		}
	}
}
