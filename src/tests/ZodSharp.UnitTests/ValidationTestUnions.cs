using Purview.Results.SourceGenerator;
using System.Collections.Immutable;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp;

// CA1815 is answered by the package's suppressor for every union opted in with [GenerateResult].
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
