using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Builds the mapper, request contexts and service providers the result-to-response tests need.
/// </summary>
static class ResultsHttpTestFactory
{
	public static DefaultResultsHttpMapper CreateMapper(Action<ResultsHttpOptions>? configure = null)
	{
		ResultsHttpOptions options = new();
		configure?.Invoke(options);

		return new DefaultResultsHttpMapper(
			Options.Create(options),
			NullLogger<DefaultResultsHttpMapper>.Instance
		);
	}

	public static DefaultHttpContext CreateContext(IServiceProvider? services = null)
	{
		DefaultHttpContext context = new() { Response = { Body = new MemoryStream() } };

		if (services is not null)
			context.RequestServices = services;

		return context;
	}

	public static ServiceProvider CreateServices(Action<ResultsHttpOptions>? configure = null)
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddResultsHttp(configure);

		return services.BuildServiceProvider();
	}

	public static async Task<Response> ExecuteAsync(IResult result, HttpContext context)
	{
		await result.ExecuteAsync(context);

		context.Response.Body.Seek(0, SeekOrigin.Begin);
		using StreamReader reader = new(context.Response.Body);
		var body = await reader.ReadToEndAsync();

		return new Response(context.Response.StatusCode, body, context.Response.ContentType);
	}

	internal sealed record Response(int StatusCode, string Body, string? ContentType);
}
