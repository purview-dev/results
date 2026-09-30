using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
	/// <param name="builder">The endpoint to add the mapping to.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static RouteHandlerBuilder WithResultsHttp(this RouteHandlerBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddEndpointFilter<ResultsEndpointFilter>();
	}

	/// <summary>
	/// Maps a <c>Result&lt;TValue, TError&gt;</c> returned by every endpoint in the group onto an ASP.NET Core
	/// response.
	/// </summary>
	/// <param name="builder">The group to add the mapping to.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
	public static RouteGroupBuilder WithResultsHttp(this RouteGroupBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddEndpointFilter<ResultsEndpointFilter>();
	}
}
