using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Purview.Results.AspNetCore;

/// <summary>
/// An <see cref="IResult"/> that maps a <see cref="Result{TValue, TError}"/> onto a response when it executes.
/// </summary>
/// <typeparam name="TValue">The type of the successful value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
/// <remarks>
/// <para>
/// Returned by <c>result.ToHttpResult()</c>. Execution resolves the registered <see cref="IResultsHttpMapper"/> from
/// the request's services, so the response is exactly the one <c>WithResultsHttp()</c> would produce: the same
/// success handling, the same failure resolution order and the same unmapped-failure response.
/// </para>
/// <para>
/// Use it instead of returning the result directly when the host is published with Native AOT. ASP.NET Core's
/// request delegate generator resolves JSON metadata for a handler's declared return type when it builds the
/// endpoint, and a <see cref="Result{TValue, TError}"/> has none: the result is never serialized, and its error is
/// usually a union that System.Text.Json's source generator cannot describe. A handler declared to return this type
/// returns an <see cref="IResult"/>, so nothing is resolved for the result itself; only <typeparamref name="TValue"/>
/// needs to be in the app's <c>JsonSerializerContext</c>.
/// </para>
/// <para>
/// It also describes the success response to OpenAPI: <typeparamref name="TValue"/> as JSON with
/// <see cref="ResultsHttpOptions.SuccessStatusCode"/>. Nothing is described when a
/// <see cref="ResultsHttpOptions.SuccessMapper"/> is set or when <typeparamref name="TValue"/> is itself an
/// <see cref="IResult"/>, because the response is then not known ahead of time. Failure responses are whatever the
/// host's mappings produce, so they are not described here; add <c>ProducesProblem(...)</c> to the endpoint.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// app.MapPost("/tenants", (TenantRegistration registration, TenantRegistrationService service) =&gt;
///     service.Register(registration).ToHttpResult());
/// </code>
/// </example>
public sealed class ResultHttpResult<TValue, TError> : IResult, IEndpointMetadataProvider
{
	internal ResultHttpResult(Result<TValue, TError> result)
	{
		Result = result;
	}

	/// <summary>
	/// Gets the result this response represents.
	/// </summary>
	public Result<TValue, TError> Result { get; }

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

		if (typeof(IResult).IsAssignableFrom(typeof(TValue)))
			return;

		var options = builder.ApplicationServices.GetService<IOptions<ResultsHttpOptions>>()?.Value;
		if (options?.SuccessMapper is not null)
			return;

		builder.Metadata.Add(
			new ProducesResponseTypeMetadata(
				options?.SuccessStatusCode ?? StatusCodes.Status200OK,
				typeof(TValue),
				["application/json"]
			)
		);
	}
}

/// <summary>
/// The execution shared by <see cref="ResultHttpResult{TValue, TError}"/> and <see cref="ResultHttpResult{TError}"/>.
/// </summary>
static class ResultHttpResultExecution
{
	internal static Task Execute(IResultValue result, HttpContext httpContext)
	{
		var mapper = httpContext.RequestServices.GetService<IResultsHttpMapper>()
			?? throw new InvalidOperationException(
				$"No {nameof(IResultsHttpMapper)} is registered. Call services.AddResultsHttp() before mapping a "
					+ "result with ToHttpResult()."
			);

		return mapper.Map(result, httpContext).ExecuteAsync(httpContext);
	}
}
