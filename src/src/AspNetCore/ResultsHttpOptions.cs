using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Configures how <see cref="IResultValue"/> values are mapped onto ASP.NET Core responses.
/// </summary>
/// <remarks>
/// Mappings are keyed by the runtime type of the error value: for a union error the most specific case wins, so
/// <c>Map&lt;TenantNotFound&gt;(...)</c> handles that case, while <c>Map&lt;TenantError&gt;(...)</c> handles every
/// case that has no mapping of its own. A nested union resolves to its innermost case, and each enclosing union
/// type is tried in turn, so <c>Map&lt;BillingError&gt;(...)</c> can handle a whole nested union while
/// <c>Map&lt;BillingUnavailable&gt;(...)</c> handles one of its leaves. A non-union error type is keyed by the error
/// type itself. Registering the same type twice replaces the earlier mapping.
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

	// Frozen snapshots taken the first time a request reads the options.
	//
	// These collections are configured at startup but stay publicly writable for the process lifetime, and
	// the mapper that reads them is a singleton. Without a snapshot, a host that calls Map<T>() after
	// startup mutates a non-concurrent Dictionary while request threads enumerate it — torn reads, or
	// "Collection was modified" from the fallback loop. Taking an immutable copy on first use makes the
	// read path safe without changing the configuration API, and makes the startup-only contract explicit:
	// a mutation after the first request is ignored rather than corrupting anything.
	// Held as the interface, not as ImmutableArray: the property returns IReadOnlyList, and an
	// ImmutableArray is a struct, so storing it as one would box on every access — once per failure, on
	// the request path.
	FrozenDictionary<Type, Func<object, HttpContext, IResult>>? _frozenMappers;
	IReadOnlyList<Func<ResultsFailureContext, IResult?>>? _frozenFallbacks;

	/// <summary>
	/// Gets or sets the status code used when a result succeeded. Defaults to <c>200 OK</c>.
	/// </summary>
	/// <remarks>
	/// This applies to a successful value result. A successful unit <see cref="Result{TError}"/> carries no
	/// payload, so it answers <c>204 No Content</c> regardless of this value; set <see cref="SuccessMapper"/> to
	/// override that.
	/// </remarks>
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
	/// <remarks>
	/// Gate this on the environment rather than setting it unconditionally, for example
	/// <c>options.ThrowOnUnmappedFailure = builder.Environment.IsDevelopment();</c>.
	/// </remarks>
	public bool ThrowOnUnmappedFailure { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the unmapped-failure response names the error type in its
	/// <c>errorType</c> extension. Defaults to <see langword="false"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The value is the error's <see cref="Type.FullName"/> — for example
	/// <c>Contoso.Billing.Internal.Domain.Errors.SubscriptionLedgerCorrupted</c>. That is internal namespace
	/// topology and domain vocabulary, returned to whoever made the request, on the exact path that fires
	/// when something unexpected happened. On an internet-facing API that is information disclosure, so it
	/// is off by default and the response carries only the status and title.
	/// </para>
	/// <para>
	/// It is genuinely useful while finding mapping gaps, so enable it per environment —
	/// <c>options.IncludeErrorTypeInProblemDetails = builder.Environment.IsDevelopment();</c>. The type name
	/// is logged either way, so you can diagnose a gap in production from your own logs without putting it
	/// in the response.
	/// </para>
	/// </remarks>
	public bool IncludeErrorTypeInProblemDetails { get; set; }

	/// <summary>
	/// Gets or sets a mapper that overrides how a successful value becomes a response. When set, it replaces the
	/// default of serializing the value with <see cref="SuccessStatusCode"/>.
	/// </summary>
	public Func<object?, HttpContext, IResult>? SuccessMapper { get; set; }

	/// <summary>
	/// Gets the fallbacks consulted, in order, when a failure has no mapping.
	/// </summary>
	/// <remarks>
	/// Reading this freezes the fallback list: these options are startup configuration, and the mapper that
	/// consults them is a singleton shared by every request. A fallback added after the first failure is
	/// handled is therefore ignored rather than mutating a list another thread is enumerating.
	/// </remarks>
	public IReadOnlyList<Func<ResultsFailureContext, IResult?>> Fallbacks =>
		_frozenFallbacks ??= ImmutableArray.CreateRange(_fallbacks);

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
		(_frozenMappers ??= _mappers.ToFrozenDictionary()).TryGetValue(caseType, out mapper!);

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
