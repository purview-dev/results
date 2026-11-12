namespace Purview.Results.SourceGenerator;

/// <summary>
/// Proves that a custom union's storage strategy is invisible to the generator, and that the non-boxing
/// access pattern — <c>HasValue</c> plus <c>TryGetValue</c> — keeps a value-type case unboxed while the
/// compiler matches it.
/// </summary>
/// <remarks>
/// The generated helpers are pure <c>Result&lt;...&gt;.Failure</c> calls, so they add no boxing of their own. A
/// compiler-generated <c>union</c> declaration boxes a value-type case in its own <c>object?</c> field; the
/// custom <see cref="CountOrLabel"/> union in <c>GeneratedTestUnions</c> stores its cases in typed fields
/// instead, which is the option to reach for when an error union is on a hot path.
/// </remarks>
public class NonBoxingUnionTests
{
	[Test]
	public async Task UnionFactory_GivenNonBoxingUnionCase_ProducesValueFailure()
	{
		// Arrange
		Count count = new(7);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<int, CountOrLabel> result = CountOrLabel.Failure<int>(count);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.TryGetValue(out Count stored)).IsTrue();
		await Assert.That(stored).IsEqualTo(count);
	}

	[Test]
	public async Task AsFailure_GivenNonBoxingUnionCase_ProducesUnitFailure()
	{
		// Arrange
		Count count = new(3);

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<CountOrLabel> result = count.AsFailure();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error.HasValue).IsTrue();
	}

	[Test]
	public async Task PatternMatching_GivenNonBoxingUnion_DoesNotBoxTheValueCase()
	{
		// Arrange — warm the JIT so the measured region covers the match, not first-call compilation.
		_ = Describe(new CountOrLabel(new Count(0)));

		// Act — one box per iteration would allocate roughly 24 KB over 1024 iterations.
		var before = GC.GetAllocatedBytesForCurrentThread();
		var total = 0;

		for (var index = 0; index < 1024; index++)
			total += Describe(new CountOrLabel(new Count(index)));

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// Assert — a boxed Count would dominate the measurement; TryGetValue(out Count) keeps it on the stack.
		await Assert.That(total).IsGreaterThan(0);
		await Assert.That(allocated).IsLessThan(1024);
	}

	static int Describe(CountOrLabel value) =>
		value switch
		{
			Count count => count.Value,
			Label label => label.Value.Length,
			null => -1,
		};
}
