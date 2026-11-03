// Declares a C# 15 union, which CSharpier cannot parse yet, so this file is listed in .csharpierignore.
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Purview.Results;
using Purview.Results.ZodSharp;
using Purview.ValueObjects.Serialization;
using ZodSharp;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace PackedResultsAot;

/// <summary>A tenant identifier whose type-level rule validates the value object as a unit.</summary>
[Scalar]
[ZodSchema]
[NonSentinel(Code = TenantId.InvalidCode, Message = "A tenant id is required.")]
public readonly partial record struct TenantId
{
	public const string InvalidCode = "invalid_tenant_id";

	public string Value { get; }
}

[ZodSchema]
public sealed class RenameTenant
{
	[Required]
	[StringLength(40, MinimumLength = 2)]
	public string? Name { get; set; }

	[MinCount(1)]
	public List<string> Owners { get; set; } = [];
}

[GenerateResult]
public readonly union TenantError(TenantInputInvalid, TenantNotFound);

public readonly record struct TenantInputInvalid(ImmutableArray<ValidationError> Errors) : IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

public readonly record struct TenantNotFound(TenantId TenantId);
