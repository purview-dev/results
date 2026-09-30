using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Purview.Results.AspNetCore;

namespace Purview.Results;

/// <summary>
/// Converts results into ASP.NET Core responses.
/// </summary>
public static class ResultHttpExtensions
{
	extension<TValue, TError>(Result<TValue, TError> result)
	{
		/// <summary>
		/// Converts the result into an ASP.NET Core response using the mapper registered in dependency injection.
		/// </summary>
		/// <param name="context">The current request.</param>
		/// <returns>The response the result represents.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
		public IResult ToHttpResult(HttpContext context)
		{
			ArgumentNullException.ThrowIfNull(context);

			return result.ToHttpResult(context.RequestServices.GetRequiredService<IResultsHttpMapper>(), context);
		}

		/// <summary>
		/// Converts the result into an ASP.NET Core response using the supplied mapper.
		/// </summary>
		/// <param name="mapper">The mapper that turns the result into a response.</param>
		/// <param name="context">The current request.</param>
		/// <returns>The response the result represents.</returns>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="mapper"/> or <paramref name="context"/> is null.
		/// </exception>
		public IResult ToHttpResult(IResultsHttpMapper mapper, HttpContext context)
		{
			ArgumentNullException.ThrowIfNull(mapper);
			ArgumentNullException.ThrowIfNull(context);

			return mapper.Map(result, context);
		}
	}
}
