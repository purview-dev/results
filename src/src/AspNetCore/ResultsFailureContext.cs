using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// The failure a mapper is asked to turn into a response.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Case"/> is the failure's <em>case</em> value: the active case of a union error, or the error
/// itself when the error is not a union. <see cref="Error"/> is the error value the result carries, so a mapper
/// that has to look at the union as a whole can, while one that only cares about the payload does not have to
/// unwrap it.
/// </para>
/// <para>
/// A context is only built for a failure; a successful result never reaches a mapper that maps failures.
/// </para>
/// </remarks>
/// <param name="Case">The union case, or the error itself when the error is not a union.</param>
/// <param name="Error">The error value the result carries.</param>
/// <param name="HttpContext">The current request.</param>
public readonly record struct ResultsFailureContext(object? Case, object? Error, HttpContext HttpContext);
