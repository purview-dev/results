// Shared by the Blazor consumers (copied in by scripts/test-packed-consumers.ps1). Declares a C# 15 union, which
// CSharpier cannot parse yet, so this file is listed in .csharpierignore.
using System.Collections.Immutable;
using Purview.Results;
using Purview.Results.ZodSharp;
using ZodSharp.Core;

namespace PackedResultsBlazor;

/// <summary>The expected failures of a registration; the packed generator emits the result helpers for it.</summary>
[GenerateResult]
public readonly union TenantError(TenantInputInvalid, TenantAlreadyExists);

/// <summary>The input did not satisfy its schema.</summary>
public readonly record struct TenantInputInvalid(ImmutableArray<ValidationError> Errors) : IValidationErrorCarrier
{
	ImmutableArray<ValidationError> IValidationErrorCarrier.ValidationErrors => Errors;
}

/// <summary>A tenant with the requested id already exists.</summary>
public readonly record struct TenantAlreadyExists(string TenantId);
