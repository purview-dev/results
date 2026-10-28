// Shared by the Blazor consumers: scripts/test-packed-consumers.ps1 copies this folder into BlazorWasm and
// BlazorServer before building them.
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Schemas;

namespace PackedResultsBlazor;

/// <summary>The form model: ZodSharp validates it, and a rejection becomes a result failure.</summary>
[ZodSchema]
public sealed partial class TenantRegistration
{
	public const string TenantIdMatchesNameCode = "tenant_id_matches_name";

	[Required]
	[RegularExpression("^[a-z0-9-]{3,20}$")]
	public string? TenantId { get; set; }

	[Required]
	[StringLength(40, MinimumLength = 2)]
	public string? Name { get; set; }

	[Required]
	[EmailAddress]
	public string? OwnerEmail { get; set; }

	partial void OnZodValidate(RefineCtx<TenantRegistration> context)
	{
		if (context.Value.TenantId is { } tenantId && tenantId == context.Value.Name)
		{
			context.AddIssue(
				TenantIdMatchesNameCode,
				"The tenant id must differ from the display name.",
				[nameof(TenantId)]
			);
		}
	}
}

/// <summary>A registered tenant.</summary>
public sealed record Tenant(string TenantId, string Name, string OwnerEmail);
