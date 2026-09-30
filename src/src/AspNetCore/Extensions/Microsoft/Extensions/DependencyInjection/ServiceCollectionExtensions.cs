using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.Results.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers result-to-response mapping.
/// </summary>
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers the mapper and endpoint filter that turn results into ASP.NET Core responses.
		/// </summary>
		/// <param name="configure">Configures the mappings for the host's own error types.</param>
		/// <returns>The service collection for chaining.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
		public IServiceCollection AddResultsHttp(Action<ResultsHttpOptions>? configure = null)
		{
			ArgumentNullException.ThrowIfNull(services);

			// Problem responses (the unmapped failure and uninitialized result cases) are written through
			// IProblemDetailsService; registering it keeps the package working without further host setup.
			services.AddProblemDetails();

			if (configure is not null)
				services.Configure(configure);

			services.TryAddSingleton<IResultsHttpMapper, DefaultResultsHttpMapper>();
			services.TryAddSingleton<ResultsEndpointFilter>();

			return services;
		}
	}
}
