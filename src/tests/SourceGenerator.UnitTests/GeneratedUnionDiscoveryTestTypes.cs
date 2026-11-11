using Purview.Unions;

namespace Purview.Results.SourceGenerator;

// Union-shaped types that stand in for a union emitted by Purview.Unions. The generator runs against this
// project as an analyzer, so these declarations prove the source-visible discovery path end to end: there is
// no [Union] attribute and no public single-parameter constructor for the results generator to read.

/// <summary>A case of the typeof-list generated-union discovery union.</summary>
/// <param name="Id">The identifier.</param>
public readonly record struct DiscoveredSourceCase(int Id);

/// <summary>A second case of the typeof-list generated-union discovery union.</summary>
/// <param name="Reason">The reason.</param>
public readonly record struct DiscoveredOtherCase(string Reason);

/// <summary>
/// A union-shaped type whose case list is only the <c>[GenerateUnion]</c> <c>typeof</c> list, which is what
/// the results generator sees for a union another generator emits.
/// </summary>
/// <remarks>
/// The implicit conversions stand in for the members the union generator emits into the final compilation,
/// so the generated helpers compile and can be executed without exposing source constructors to discovery.
/// </remarks>
[GenerateUnion(typeof(DiscoveredSourceCase), typeof(DiscoveredOtherCase))]
[GenerateResult]
public readonly struct DiscoveredError
{
	DiscoveredError(object? value) => Value = value;

	/// <summary>Converts a case into the union.</summary>
	/// <param name="value">The case value.</param>
	public static implicit operator DiscoveredError(DiscoveredSourceCase value) => new(value);

	/// <summary>Converts a case into the union.</summary>
	/// <param name="value">The case value.</param>
	public static implicit operator DiscoveredError(DiscoveredOtherCase value) => new(value);

	/// <summary>Gets the boxed case value.</summary>
	public object? Value { get; }
}

/// <summary>A case of the factory-list generated-union discovery union.</summary>
/// <param name="Id">The identifier.</param>
public readonly record struct FactoryDiscoveredCase(int Id);

/// <summary>A second case of the factory-list generated-union discovery union.</summary>
/// <param name="Reason">The reason.</param>
public readonly record struct FactoryDiscoveredOtherCase(string Reason);

/// <summary>
/// A union-shaped type whose case list is only its partial static factory signatures. The signatures are the
/// source-visible case list; the implementations exist only so the test compilation is valid.
/// </summary>
[GenerateUnion]
[GenerateResult]
public readonly partial struct FactoryDiscoveredError
{
	FactoryDiscoveredError(object? value) => Value = value;

	/// <summary>Creates the union from a case.</summary>
	/// <param name="value">The case value.</param>
	/// <returns>The union holding the case.</returns>
	public static partial FactoryDiscoveredError FromFactoryDiscoveredCase(FactoryDiscoveredCase value);

	/// <summary>Creates the union from a case.</summary>
	/// <param name="value">The case value.</param>
	/// <returns>The union holding the case.</returns>
	public static partial FactoryDiscoveredError FromFactoryDiscoveredCase(FactoryDiscoveredCase value) => new(value);

	/// <summary>Creates the union from a case.</summary>
	/// <param name="value">The case value.</param>
	/// <returns>The union holding the case.</returns>
	public static partial FactoryDiscoveredError FromFactoryDiscoveredOtherCase(FactoryDiscoveredOtherCase value);

	/// <summary>Creates the union from a case.</summary>
	/// <param name="value">The case value.</param>
	/// <returns>The union holding the case.</returns>
	public static partial FactoryDiscoveredError FromFactoryDiscoveredOtherCase(FactoryDiscoveredOtherCase value) =>
		new(value);

	/// <summary>Converts a case into the union.</summary>
	/// <param name="value">The case value.</param>
	public static implicit operator FactoryDiscoveredError(FactoryDiscoveredCase value) => new(value);

	/// <summary>Converts a case into the union.</summary>
	/// <param name="value">The case value.</param>
	public static implicit operator FactoryDiscoveredError(FactoryDiscoveredOtherCase value) => new(value);

	/// <summary>Gets the boxed case value.</summary>
	public object? Value { get; }
}
