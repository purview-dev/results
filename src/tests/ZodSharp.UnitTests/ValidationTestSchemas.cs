// The test namespace (Purview.Results.ZodSharp) shadows the ZodSharp namespace, so the using is global-qualified.
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Schemas;

namespace Purview.Results.ZodSharp;

/// <summary>
/// An operation input whose schema rejects a missing name, so a validation failure is produced with a known path.
/// </summary>
[ZodSchema]
public sealed record Operation
{
	[Required]
	public string? Name { get; init; }

	public static Operation Create(string? name) => new() { Name = name };
}

/// <summary>
/// An operation result whose schema declares a cross-member invariant, so the refinement hook produces a known
/// error code.
/// </summary>
[ZodSchema]
public sealed partial record OperationResult
{
	public int Discovered { get; init; }

	public int Completed { get; init; }

	public int Skipped { get; init; }

	public static OperationResult Create(int discovered, int completed, int skipped) =>
		new()
		{
			Discovered = discovered,
			Completed = completed,
			Skipped = skipped,
		};

	partial void OnZodValidate(RefineCtx<OperationResult> context)
	{
		if (context.Value.Discovered != (context.Value.Completed + context.Value.Skipped))
		{
			context.AddIssue(
				"invalid_operation_result",
				"Discovered must equal Completed and Skipped.",
				[nameof(Discovered)]
			);
		}
	}
}
