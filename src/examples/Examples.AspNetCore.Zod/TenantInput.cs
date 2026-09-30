using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Schemas;

namespace Purview.Results.Examples.AspNetCore.Zod;

/// <summary>
/// The raw input a caller registers a tenant with.
/// </summary>
/// <remarks>
/// <c>[ZodSchema]</c> makes ZodSharp's generator emit <c>TenantInputSchema</c>, whose <c>Validate</c> method
/// returns a <c>ValidationResult&lt;TenantInput&gt;</c> instead of throwing.
/// </remarks>
[ZodSchema]
public sealed partial record TenantInput
{
	/// <summary>The identifier of the new tenant.</summary>
	[Required]
	public string? TenantId { get; init; }

	/// <summary>The display name of the new tenant.</summary>
	[Required]
	public string? Name { get; init; }

	/// <summary>
	/// Creates an input.
	/// </summary>
	/// <param name="tenantId">The identifier of the new tenant.</param>
	/// <param name="name">The display name of the new tenant.</param>
	/// <returns>The input.</returns>
	public static TenantInput Create(string? tenantId, string? name) => new() { TenantId = tenantId, Name = name };

	partial void OnZodValidate(RefineCtx<TenantInput> context)
	{
		if (context.Value.TenantId is not null && context.Value.TenantId == context.Value.Name)
		{
			context.AddIssue(
				"tenant_id_matches_name",
				"The tenant id must differ from the display name.",
				[nameof(TenantId)]
			);
		}
	}
}
