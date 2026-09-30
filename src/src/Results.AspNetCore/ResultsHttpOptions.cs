using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Configures how <see cref="IResultValue"/> values are mapped onto ASP.NET Core responses.
/// </summary>
/// <remarks>
/// Mappings are keyed by the runtime type of the error value: for a union error the declared case type is used,
/// so <c>Map&lt;TenantNotFound&gt;(...)</c> handles that case, while <c>Map&lt;TenantError&gt;(...)</c> handles every
/// case that has no mapping of its own. A non-union error type is keyed by the error type itself. Registering the
/// same type twice replaces the earlier mapping.
/// </remarks>
public sealed class ResultsHttpOptions
{
	readonly Dictionary<Type, Func<object, HttpContext, IResult>> _mappers = [];
	readonly List<Func<object?, HttpContext, IResult?>> _fallbacks = [];

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
	/// Gets or sets the title of the <see cref="ProblemDetails"/> produced for an unmapped failure.
	/// </summary>
	public string UnmappedTitle { get; set; } =
		"The operation failed with an error that is not mapped to an HTTP response.";

	/// <summary>
	/// Gets or sets a value indicating whether produced problem responses carry the request's trace identifier.
	/// Defaults to <see langword="true"/>.
	/// </summary>
	public bool IncludeTraceId { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether an unmapped failure throws instead of producing a problem response.
	/// Defaults to <see langword="false"/>; set it during development to surface mapping gaps early.
	/// </summary>
	public bool ThrowOnUnmappedFailure { get; set; }

	/// <summary>
	/// Gets or sets a mapper that overrides how a successful value becomes a response. When set, it replaces the
	/// default of serializing the value with <see cref="ResultsHttpOptions.SuccessStatusCode"/>.
	/// </summary>
	public Func<object?, HttpContext, IResult>? SuccessMapper { get; set; }

	/// <summary>
	/// Gets the fallbacks consulted, in order, when a failure has no mapping.
	/// </summary>
	public IReadOnlyList<Func<object?, HttpContext, IResult?>> Fallbacks => _fallbacks;

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

		_fallbacks.Add(fallback);

		return this;
	}

	internal bool TryGetMapper(Type caseType, out Func<object, HttpContext, IResult> mapper) =>
		_mappers.TryGetValue(caseType, out mapper!);
}
