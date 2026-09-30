using System.Collections.Immutable;
using Purview.Results.SourceGeneration;
using global::ZodSharp.Core;

namespace Purview.Results.ZodSharp;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types")]
[GenerateResult]
public readonly union OperationError(OperationRejected, OperationResultInvalid);

/// <summary>
/// An operation was rejected before it ran because its input did not satisfy its schema.
/// </summary>
/// <param name="Operation">The rejected input.</param>
/// <param name="Errors">The validation errors reported by <c>OperationSchema</c>.</param>
public readonly record struct OperationRejected(Operation Operation, ImmutableArray<ValidationError> Errors);

/// <summary>
/// An operation produced an aggregate that failed its cross-member invariant.
/// </summary>
/// <param name="Result">The inconsistent aggregate.</param>
/// <param name="Errors">The validation errors reported by <c>OperationResultSchema</c>.</param>
public readonly record struct OperationResultInvalid(
	OperationResult Result,
	ImmutableArray<ValidationError> Errors
);
