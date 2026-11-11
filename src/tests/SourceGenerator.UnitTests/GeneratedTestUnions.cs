using System.Runtime.CompilerServices;

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
/// <remarks>
/// CA1815 is not raised for this declaration: the package's suppressor answers it for every union opted in
/// with <c>[GenerateResult]</c>, so no pragma or suppress-message attribute is needed here.
/// </remarks>
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);

/// <summary>A billing account does not exist.</summary>
/// <param name="AccountId">The identifier of the billing account.</param>
public readonly record struct BillingAccountMissing(string AccountId);

/// <summary>The billing service is unavailable.</summary>
/// <param name="Reason">The reason the billing service is unavailable.</param>
public readonly record struct BillingServiceUnavailable(string Reason);

/// <summary>The errors a billing operation can produce.</summary>
/// <remarks>
/// This union stands in for a service boundary: a tenant operation consumes it and widens its error into
/// <see cref="RegisterTenantError"/> rather than leaking the billing case types.
/// </remarks>
[GenerateResult]
public readonly union BillingError(BillingAccountMissing, BillingServiceUnavailable);

/// <summary>
/// The errors a tenant registration can produce: the whole <see cref="TenantError"/> and
/// <see cref="BillingError"/> unions, each as a single case.
/// </summary>
/// <remarks>
/// The case types are unions, not their leaf cases. Each is an <em>included union</em>: the generated factory
/// also covers its cases and constructs the nested value, so a billing failure can be lifted into this contract
/// with one union conversion and no leaf case is shared with <see cref="BillingError"/>.
/// </remarks>
[GenerateResult]
public readonly union RegisterTenantError(TenantError, BillingError);

/// <summary>A count, stored unboxed by <see cref="CountOrLabel"/>.</summary>
/// <param name="Value">The count value.</param>
public readonly record struct Count(int Value);

/// <summary>A label, stored unboxed by <see cref="CountOrLabel"/>.</summary>
/// <param name="Value">The label text.</param>
public readonly record struct Label(string Value);

/// <summary>
/// A custom union that uses the non-boxing access pattern: its value-type cases live in typed fields, and
/// <see cref="HasValue"/> plus <see cref="TryGetValue(out Count)"/> let the compiler's pattern matching avoid
/// boxing them.
/// </summary>
/// <remarks>
/// A compiler-generated <c>union</c> declaration always stores its cases as <see cref="object"/>, so a
/// value-type case boxes. A manually declared <c>[Union]</c> type with typed storage is the supported way to
/// keep the case unboxed in a hot path, and the generator treats it like any other union: its per-case
/// <c>AsFailure</c> helpers and union-receiver factory are still emitted. The allocation test in
/// <c>NonBoxingUnionTests</c> proves the switch below does not box.
/// </remarks>
[Union]
[GenerateResult]
public readonly struct CountOrLabel : IUnion
{
	readonly Count _count;
	readonly Label? _label;
	readonly byte _tag;

	/// <summary>Creates a union holding a <see cref="Count"/>.</summary>
	/// <param name="value">The count to hold.</param>
	public CountOrLabel(Count value)
	{
		_count = value;
		_label = null;
		_tag = 1;
	}

	/// <summary>Creates a union holding a <see cref="Label"/>.</summary>
	/// <param name="value">The label to hold.</param>
	public CountOrLabel(Label value)
	{
		_count = default;
		_label = value;
		_tag = value.Value is null ? (byte)0 : (byte)2;
	}

	/// <inheritdoc />
	public object? Value =>
		_tag switch
		{
			1 => _count,
			2 => _label,
			_ => null,
		};

	/// <summary>Gets a value indicating whether the union holds a non-null case.</summary>
	public bool HasValue => _tag != 0;

	/// <summary>Attempts to read the <see cref="Count"/> case without boxing it.</summary>
	/// <param name="value">The count when the union holds one; otherwise <see langword="default"/>.</param>
	/// <returns><see langword="true"/> when the union holds a <see cref="Count"/>.</returns>
	public bool TryGetValue(out Count value)
	{
		value = _count;

		return _tag == 1;
	}

	/// <summary>Attempts to read the <see cref="Label"/> case without boxing it.</summary>
	/// <param name="value">The label when the union holds one; otherwise <see langword="default"/>.</param>
	/// <returns><see langword="true"/> when the union holds a <see cref="Label"/>.</returns>
	public bool TryGetValue(out Label value)
	{
		value = _label.GetValueOrDefault();

		return _tag == 2;
	}
}
