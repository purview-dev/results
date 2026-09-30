using Purview.Results.SourceGeneration;
using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types")]
[GenerateResult]
public readonly union ValidationTestError(InputRejected, AggregateRejected, ItemMissing);

/// <summary>
/// An input that was rejected by its schema, carrying the reported validation errors.
/// </summary>
/// <param name="ItemId">The identifier of the rejected input.</param>
/// <param name="Errors">The validation errors of the input's schema.</param>
public readonly record struct InputRejected(int ItemId, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// An aggregate that failed its cross-member invariant, carrying the reported validation errors.
/// </summary>
/// <param name="Aggregate">The name of the aggregate that failed.</param>
/// <param name="Errors">The validation errors of the aggregate's schema.</param>
public readonly record struct AggregateRejected(string Aggregate, ImmutableArray<ValidationError> Errors)
	: IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>
/// An error that carries no validation errors, so it is not mapped by the ZodSharp mapping.
/// </summary>
/// <param name="ItemId">The identifier of the missing item.</param>
public readonly record struct ItemMissing(int ItemId);
