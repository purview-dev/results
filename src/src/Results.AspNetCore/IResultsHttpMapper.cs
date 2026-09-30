using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Maps a result onto an ASP.NET Core response.
/// </summary>
public interface IResultsHttpMapper
{
	/// <summary>
	/// Maps the result onto an ASP.NET Core response.
	/// </summary>
	/// <param name="result">The result to map.</param>
	/// <param name="context">The current request.</param>
	/// <returns>The response the result represents.</returns>
	IResult Map(IResultValue result, HttpContext context);
}
