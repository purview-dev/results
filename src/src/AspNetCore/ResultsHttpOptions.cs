using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Configures how <see cref="IResultValue"/> values are mapped onto ASP.NET Core responses.
/// </summary>
/// <remarks>
/// Mappings are keyed by the runtime type of the error value: for a union error the declared case type is used,
/// so <c>Map&lt;TenantNotFound&gt;(...)</c> handles that case, while <c>Map&lt;TenantError&gt;(...)</c> handles every
/// case that has no mapping of its own. A non-union error type is keyed by the error type itself. Registering the
/// same type twice replaces the earlier mapping.
/// <para>
/// A failure that no mapping handled reaches the fallback stage, which is the single ordered list
/// <see cref="AddFallback"/> and <see cref="AddFailureMapper{TMapper}"/> append to: the order they are called in
/// is the order they are consulted in, and a failure no fallback answers produces the unmapped-failure response.
/// </para>
/// </remarks>
public sealed class ResultsHttpOptions
{
	readonly Dictionary<Type, Func<object, HttpContext, IResult>> _mappers = [];
	readonly List<Func<ResultsFailureContext, IResult?>> _fallbacks = [];

	/// <summary>
	/// Gets or sets the status code used when a result succeeded. Defaults to <c>200 OK</c>.
	/// </summary>
	public int SuccessStatusCode { get; set; } = StatusCodes.Status200OK;

	/// <summary>
	/// Gets or sets the status code used when a failure has no mapping. Defaults to
	/// <c>500 Internal Server Error</c>, because an unmapped failure is a mapping gap in the host.
	/// </summary>
	public int UnmappedStatusCode { get; set; } = StatusCodes.Status500InternalServerError;

	/// <summary>
	/// Gets or sets the title of the <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> produced for an unmapped
	/// failure.
	/// </summary>
	public string UnmappedTitle { get; set; } =
		"The operation failed with an error that is not mapped to an HTTP response.";

	/// <summary>
	/// Gets or sets a value indicating whether problem responses this package serializes itself carry the
	/// request's trace identifier. Defaults to <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// ASP.NET Core adds a trace identifier to <c>ProblemDetails</c> responses it writes, so this option applies to
	/// the responses this package writes itself — a validation problem, for example.
	/// </remarks>
	public bool IncludeTraceId { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether an unmapped failure throws instead of producing a problem response.
	/// Defaults to <see langword="false"/>; set it during development to surface mapping gaps early.
	/// </summary>
	public bool ThrowOnUnmappedFailure { get; set; }

	/// <summary>
	/// Gets or sets a mapper that overrides how a successful value becomes a response. When set, it replaces the
	/// default of serializing the value with <see cref="SuccessStatusCode"/>.
	/// </summary>
	public Func<object?, HttpContext, IResult>? SuccessMapper { get; set; }

	/// <summary>
	/// Gets the fallbacks consulted, in order, when a failure has no mapping.
	/// </summary>
	public IReadOnlyList<Func<ResultsFailureContext, IResult?>> Fallbacks => _fallbacks;

	/// <summary>
	/// Maps a case (or the error itself, for a non-union error type) onto a response.
	/// </summary>
	/// <typeparam name="TCase">The case type to map.</typeparam>
	/// <param name="map">Creates the response for the case.</param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	public ResultsHttpOptions Map<TCase>(Func<TCase, IResult> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return Map<TCase>((value, _) => map(value));
	}

	/// <summary>
	/// Maps a case (or the error itself, for a non-union error type) onto a response, with access to the request.
	/// </summary>
	/// <typeparam name="TCase">The case type to map.</typeparam>
	/// <param name="map">Creates the response for the case and request context.</param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	public ResultsHttpOptions Map<TCase>(Func<TCase, HttpContext, IResult> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		_mappers[typeof(TCase)] = (error, context) => map((TCase)error, context);

		return this;
	}

	/// <summary>
	/// Adds a fallback that is consulted, in registration order, when a failure has no mapping. A fallback returns
	/// <see langword="null"/> to defer to the next fallback.
	/// </summary>
	/// <param name="fallback">Creates a response for an unmapped failure, or returns <see langword="null"/>.</param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="fallback"/> is null.</exception>
	public ResultsHttpOptions AddFallback(Func<object?, HttpContext, IResult?> fallback)
	{
		ArgumentNullException.ThrowIfNull(fallback);

		// A fallback sees the case, so per-error-type behaviour does not have to unwrap a union itself.
		_fallbacks.Add(context => fallback(context.Case, context.HttpContext));

		return this;
	}

	/// <summary>
	/// Adds a failure mapper that is consulted, in registration order, alongside the fallbacks when a failure has
	/// no mapping.
	/// </summary>
	/// <typeparam name="TMapper">The mapper type, registered in dependency injection.</typeparam>
	/// <returns>The options instance for chaining.</returns>
	/// <remarks>
	/// The mapper is resolved from the failing request's services the first time it is needed, so it may take its
	/// own dependencies in its constructor. Register it before the first request — for example
	/// <c>services.AddSingleton&lt;MyMapper&gt;()</c>, or against <see cref="IResultsFailureMapper"/> and name that
	/// interface as <typeparamref name="TMapper"/>.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a failure reaches the mapper and it is not registered in dependency injection.
	/// </exception>
	public ResultsHttpOptions AddFailureMapper<TMapper>()
		where TMapper : class, IResultsFailureMapper
	{
		_fallbacks.Add(context => ResolveMapper<TMapper>(context.HttpContext.RequestServices).Map(context));

		return this;
	}

	internal bool TryGetMapper(Type caseType, out Func<object, HttpContext, IResult> mapper) =>
		_mappers.TryGetValue(caseType, out mapper!);

	/// <summary>
	/// Resolves a declared failure mapper, reporting the registration the host is missing rather than the
	/// dependency injection container's own message.
	/// </summary>
	static TMapper ResolveMapper<TMapper>(IServiceProvider services)
		where TMapper : class, IResultsFailureMapper =>
		services.GetService<TMapper>()
		?? throw new InvalidOperationException(
			$"The failure mapper '{typeof(TMapper).FullName}' is declared in ResultsHttpOptions but is not registered in dependency injection. Register it, for example services.AddSingleton<{typeof(TMapper).Name}>()."
		);
}
