using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Maps a failure onto a response by looking at the failure's <em>shape</em> rather than at its type.
/// </summary>
/// <remarks>
/// <para>
/// A mapping registered on <see cref="ResultsHttpOptions"/> is keyed by type, which covers the cases a host can
/// name. A failure mapper is the extension point for the policies a type cannot express: rendering some of a
/// validation failure's error codes differently, giving one category of errors its own status, or any rule that
/// has to inspect the value the failure carries.
/// </para>
/// <para>
/// Mappers run in the fallback stage of the failure resolution order, so a mapping registered for the case type
/// or for the error type always wins. Return <see langword="null"/> to defer to the next fallback; the last
/// fallback that returns a response wins, and a failure that no mapping, fallback or mapper handles is the
/// unmapped-failure response (a logged <c>500</c> by default).
/// </para>
/// <para>
/// A mapper is <em>not</em> a catch-all: answering every failure here — with a generic problem or with
/// <c>202</c>, for example — turns a mapping gap in the host into a plausible-looking response, which is
/// exactly what the unmapped-failure path exists to expose. Map the shapes you can name, and answer
/// <see langword="null"/> for the rest.
/// </para>
/// <para>
/// Register a mapper with <see cref="ResultsHttpOptions.AddFailureMapper{TMapper}"/>, which resolves it from
/// dependency injection the first time it is needed, so it may take its own dependencies in its constructor.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class ValidationCodeMapper(IOptions&lt;ZodResultsHttpOptions&gt; codes) : IResultsFailureMapper
/// {
///     public IResult? Map(ResultsFailureContext context) =&gt;
///         context.Case is IValidationErrorCarrier carrier &amp;&amp; carrier.ValidationErrors.Any(e =&gt; e.Code == "not_found")
///             ? TypedResults.NotFound()
///             : null;
/// }
/// </code>
/// </example>
public interface IResultsFailureMapper
{
	/// <summary>
	/// Maps the failure onto a response.
	/// </summary>
	/// <param name="context">The failure to map and the request it belongs to.</param>
	/// <returns>The response, or <see langword="null"/> to defer to the next fallback.</returns>
	IResult? Map(ResultsFailureContext context);
}
