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
		/// Wraps the result in an <see cref="IResult"/> that is mapped onto a response when it executes.
		/// </summary>
		/// <returns>The response the result represents, mapped by the registered <see cref="IResultsHttpMapper"/>.</returns>
		/// <remarks>
		/// This is the form to return from a minimal-API handler in a Native AOT host: the handler then returns an
		/// <see cref="IResult"/>, so ASP.NET Core resolves no JSON metadata for the result type when it builds the
		/// endpoint. See <see cref="ResultHttpResult{TValue, TError}"/>.
		/// </remarks>
		public ResultHttpResult<TValue, TError> ToHttpResult() => new(result);

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

	extension<TError>(Result<TError> result)
	{
		/// <summary>
		/// Wraps the unit result in an <see cref="IResult"/> that is mapped onto a response when it executes.
		/// </summary>
		/// <returns>The response the result represents, mapped by the registered <see cref="IResultsHttpMapper"/>.</returns>
		/// <remarks>
		/// A success answers <c>204 No Content</c>. This is the form to return from a minimal-API handler in a Native
		/// AOT host; see <see cref="ResultHttpResult{TError}"/>.
		/// </remarks>
		public ResultHttpResult<TError> ToHttpResult() => new(result);

		/// <summary>
		/// Converts the unit result into an ASP.NET Core response using the mapper registered in dependency injection.
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
		/// Converts the unit result into an ASP.NET Core response using the supplied mapper.
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
