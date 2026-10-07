using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Maps a result returned by an endpoint handler onto an ASP.NET Core response.
/// </summary>
/// <remarks>
/// Register it on an endpoint or group with <c>WithResultsHttp()</c>. Handlers can then return
/// <c>Result&lt;TValue, TError&gt;</c> or a unit <c>Result&lt;TError&gt;</c> directly; a handler that returns
/// something else is left untouched.
/// </remarks>
public sealed class ResultsEndpointFilter(IResultsHttpMapper mapper) : IEndpointFilter
{
	/// <inheritdoc />
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		var result = await next(context);

		return result is IResultValue value ? mapper.Map(value, context.HttpContext) : result;
	}
}
