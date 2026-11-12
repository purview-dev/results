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

/// <summary>A count, stored unboxed by <see cref="CountOrLabel"/>.</summary>/// <param name="Value">The count value.</param>
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

/// <summary>The empty case of a generic union.</summary>
public readonly record struct None;

/// <summary>A present value of a generic union.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value.</param>
public readonly record struct Some<T>(T Value);

/// <summary>
/// A generic C# 15 union. The generator emits the union-receiver factory in a generic extension block and
/// declares the union's own type parameter on every per-case helper.
/// </summary>
[GenerateResult]
public readonly union Maybe<T>(None, Some<T>);

/// <summary>The empty case of a constrained generic union.</summary>
public readonly record struct ConstrainedNone;

/// <summary>A present value of a constrained generic union.</summary>
/// <typeparam name="T">The reference-type value.</typeparam>
/// <param name="Value">The value.</param>
public readonly record struct ConstrainedSome<T>(T Value);

/// <summary>A generic union with a bare type-parameter case, which is factory-only.</summary>
/// <typeparam name="T">The value or text.</typeparam>
// RSG1006 is deliberate here: NullableValue also owns a string case (string? normalises to string), so
// it keeps the per-case AsFailure helper and TypedOrText<T> uses the shared-case safe factory instead.
// GenericUnionTests exercises TypedOrText<int>.Failure<int, int>(...) on that path. Suppressed, with the
// full reason, by the RSG1006 NoWarn entry in SourceGenerator.UnitTests.csproj.
[GenerateResult]
public readonly union TypedOrText<T>(T, string);

/// <summary>A union with a nullable value-type case and a nullable reference-type case.</summary>
[GenerateResult]
public readonly union NullableValue(int?, string?);

/// <summary>The empty case of an included generic union.</summary>
public readonly record struct InnerNone;

/// <summary>A present value of an included generic union.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value.</param>
public readonly record struct InnerSome<T>(T Value);

/// <summary>A generic union used as a case of another generic union.</summary>
/// <typeparam name="T">The value type.</typeparam>
[GenerateResult]
public readonly union InnerError<T>(InnerNone, InnerSome<T>);

/// <summary>A text case distinct to the outer union.</summary>
/// <param name="Value">The text.</param>
public readonly record struct OuterText(string Value);

/// <summary>A generic union that includes another generic union as one case.</summary>
/// <typeparam name="T">The value type.</typeparam>
[GenerateResult]
public readonly union OuterError<T>(InnerError<T>, OuterText);

/// <summary>A case of a class-based union.</summary>
/// <param name="Id">The identifier.</param>
public sealed record ClassCase(int Id);

/// <summary>A class-based custom union, which uses reference semantics and a public constructor.</summary>
/// <remarks>Creates the union from its case.</remarks>
/// <param name="value">The case value.</param>
[Union]
[GenerateResult]
public class ClassUnion(ClassCase value) : IUnion
{
	/// <inheritdoc />
	public object? Value { get; } = value;
}

/// <summary>A generic union with a constraint that the generated extension block must repeat.</summary>
/// <typeparam name="T">The reference-type value.</typeparam>
[GenerateResult]
public readonly union ConstrainedMaybe<T>(ConstrainedNone, ConstrainedSome<T>)
	where T : class;

/// <summary>A named case of a member-provider union.</summary>
/// <param name="Id">The identifier.</param>
public readonly record struct ProvidedCase(int Id);

/// <summary>
/// A custom union whose creation members live on a nested <c>IUnionMembers</c> provider, which is the shape
/// the compiler uses when a union needs a private constructor.
/// </summary>
/// <remarks>
/// The provider is public and declares a single-parameter static <c>Create</c> method per case, so the
/// generator reads the case list from the provider rather than from constructors and constructs values
/// through <c>IUnionMembers.Create</c>.
/// </remarks>
[Union]
[GenerateResult]
public readonly struct ProvidedError : IUnion, ProvidedError.IUnionMembers
{
	ProvidedError(object? value) => Value = value;

	/// <summary>Provides the union's creation and access members.</summary>
	public interface IUnionMembers
	{
		/// <summary>Creates the union from its case.</summary>
		/// <param name="value">The case value.</param>
		/// <returns>The union holding the case.</returns>
		static ProvidedError Create(ProvidedCase value) => new(value);

		/// <summary>Gets the boxed case value.</summary>
		object? Value { get; }
	}

	object? IUnionMembers.Value => Value;

	/// <inheritdoc />
	public object? Value { get; }
}

/// <summary>A named case of a generic member-provider union.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value.</param>
public readonly record struct ProvidedSome<T>(T Value);

/// <summary>The empty case of a generic member-provider union.</summary>
public readonly record struct ProvidedNone;

/// <summary>A generic custom union whose creation members live on an <c>IUnionMembers</c> provider.</summary>
/// <typeparam name="T">The value type.</typeparam>
[Union]
[GenerateResult]
public readonly struct ProvidedOption<T> : IUnion, ProvidedOption<T>.IUnionMembers
{
	ProvidedOption(object? value) => Value = value;

	/// <summary>Provides the union's creation and access members.</summary>
	public interface IUnionMembers
	{
		/// <summary>Creates the union from a present value.</summary>
		/// <param name="value">The present value.</param>
		/// <returns>The union holding the value.</returns>
		static ProvidedOption<T> Create(ProvidedSome<T> value) => new(value);

		/// <summary>Creates the union from its empty case.</summary>
		/// <param name="value">The empty case.</param>
		/// <returns>The empty union.</returns>
		static ProvidedOption<T> Create(ProvidedNone value) => new(value);

		/// <summary>Gets the boxed case value.</summary>
		object? Value { get; }
	}

	object? IUnionMembers.Value => Value;

	/// <inheritdoc />
	public object? Value { get; }
}
