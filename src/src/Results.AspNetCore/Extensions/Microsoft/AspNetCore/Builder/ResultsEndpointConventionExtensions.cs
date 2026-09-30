using Microsoft.AspNetCore.Http;
using Purview.Results.AspNetCore;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Adds result mapping to endpoints and endpoint groups.
/// </summary>
public static class ResultsEndpointConventionExtensions
{
	/// <summary>
	/// Maps a <c>Result&lt;TValue, TError&gt;</c> returned by the endpoint onto an ASP.NET Core response.
	/// </summary>
	/// <typeparam name="TBuilder">The endpoint convention builder.</typeparam>
	/// <param name="builder">The endpoint or group to add the mapping to.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static TBuilder WithResultsHttp<TBuilder>(this TBuilder builder)
		where TBuilder : IEndpointConventionBuilder
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddEndpointFilter<ResultsEndpointFilter>();
	}
}
