using Purview.Results.SourceGeneration;

namespace Purview.Results.SourceGenerator;

// Union declarations used by the generated-code runtime tests. They live in the test project so the
// generator runs against a real compilation and the generated helpers are executed, not just compiled.

/// <summary>The identifier of a tenant.</summary>
/// <param name="Value">The identifier value.</param>
public readonly record struct TenantId(string Value);

/// <summary>The tenant that was requested does not exist.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantNotFound(TenantId TenantId);

/// <summary>The tenant that was requested is disabled.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantDisabled(TenantId TenantId);

/// <summary>The tenant that was requested already exists.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct TenantAlreadyExists(TenantId TenantId);

/// <summary>A tenant.</summary>
/// <param name="TenantId">The identifier of the tenant.</param>
public readonly record struct Tenant(TenantId TenantId);

/// <summary>The errors a tenant operation can produce.</summary>
// The C# union declaration does not synthesise value equality, and these test types are compared by case
// in the tests, so CA1815 is not meaningful here.
#pragma warning disable CA1815
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
#pragma warning restore CA1815
