using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Purview.Results.Examples.Blazor.Wasm;

/// <summary>
/// The form the Blazor sample submits.
/// </summary>
/// <remarks>
/// <c>[ZodSchema]</c> emits <c>TenantInputSchema</c> at compile time, so validation runs with no reflection and
/// is safe in a trimmed WebAssembly client.
/// </remarks>
[ZodSchema]
sealed class TenantInput
{
	/// <summary>Gets or sets the identifier of the tenant.</summary>
	[Required]
	[StringLength(40, MinimumLength = 2)]
	public string? TenantId { get; set; }

	/// <summary>Gets or sets the display name of the tenant.</summary>
	[Required]
	[StringLength(60, MinimumLength = 2)]
	public string? Name { get; set; }
}
