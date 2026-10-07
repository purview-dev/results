namespace Purview.Results;

/// <summary>
/// Represents the successful outcome of an operation that produces no value.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Result{TError}"/> is a result whose success carries no payload; it succeeds with this marker
/// instead of a value, so an operation that only has a meaningful failure — a command that either completes or
/// reports why it could not — can still be expressed as a result.
/// </para>
/// <para>
/// The type has no state: every <see cref="Success"/> value equals <see cref="Instance"/> and <c>default</c>,
/// and its <see cref="ToString"/> is the stable text <c>"Success"</c>.
/// </para>
/// </remarks>
public readonly record struct Success
{
	/// <summary>
	/// Gets the single <see cref="Success"/> value.
	/// </summary>
	public static Success Instance => default;

	/// <inheritdoc />
	public override string ToString() => "Success";
}
