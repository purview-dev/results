using System.Collections.Immutable;
using global::ZodSharp.Core;

namespace Purview.Results.ZodSharp;

/// <summary>
/// Implemented by an error value that carries ZodSharp validation errors.
/// </summary>
/// <remarks>
/// A union case that carries the errors of a rejected input implements this interface, so an HTTP layer can turn
/// it into a validation problem without knowing the error type. The errors preserve each
/// <see cref="ValidationError"/>'s code, category, path and parameters.
/// </remarks>
public interface IValidationErrorCarrier
{
	/// <summary>
	/// Gets the validation errors the error value carries.
	/// </summary>
	ImmutableArray<ValidationError> ValidationErrors { get; }
}
