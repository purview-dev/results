using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Purview.Results.AspNetCore;

/// <summary>
/// An <see cref="IResult"/> that maps a unit <see cref="Result{TError}"/> onto a response when it executes.
/// </summary>
/// <typeparam name="TError">The type of the error.</typeparam>
/// <remarks>
/// <para>
/// The unit-result counterpart of <see cref="ResultHttpResult{TValue, TError}"/>, returned by
/// <c>result.ToHttpResult()</c>: execution uses the registered <see cref="IResultsHttpMapper"/>, so a success
/// answers <c>204 No Content</c> and a failure is mapped exactly as <c>WithResultsHttp()</c> maps it. Return it from
/// a handler in a Native AOT host, where a handler declared to return the result itself fails when the endpoint is
/// built.
/// </para>
/// <para>
/// It describes the <c>204</c> success to OpenAPI, unless a <see cref="ResultsHttpOptions.SuccessMapper"/> is set.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// app.MapDelete("/tenants/{tenantId}", (string tenantId, TenantService service) =&gt;
///     service.Remove(tenantId).ToHttpResult());
/// </code>
/// </example>
public sealed class ResultHttpResult<TError> : IResult, IEndpointMetadataProvider
{
	internal ResultHttpResult(Result<TError> result)
	{
		Result = result;
	}

	/// <summary>
	/// Gets the result this response represents.
	/// </summary>
	public Result<TError> Result { get; }

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContext"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no <see cref="IResultsHttpMapper"/> is registered; call <c>AddResultsHttp()</c>.
	/// </exception>
	public Task ExecuteAsync(HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		return ResultHttpResultExecution.Execute(Result, httpContext);
	}

	/// <inheritdoc />
	static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (builder.ApplicationServices.GetService<IOptions<ResultsHttpOptions>>()?.Value.SuccessMapper is not null)
			return;

		builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status204NoContent));
	}
}
