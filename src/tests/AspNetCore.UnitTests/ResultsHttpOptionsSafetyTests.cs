using Microsoft.AspNetCore.Http;
using Purview.Results.AspNetCore;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Covers the information-disclosure and shared-state behaviour of <see cref="ResultsHttpOptions"/>.
/// </summary>
public class ResultsHttpOptionsSafetyTests
{
	[Test]
	public async Task IncludeErrorTypeInProblemDetails_DefaultsToOff()
	{
		// The unmapped-failure path fires when something unexpected happened, and the extension would carry
		// the error's full CLR type name to whoever made the request. That has to be opt-in.
		ResultsHttpOptions options = new();

		await Assert.That(options.IncludeErrorTypeInProblemDetails).IsFalse();
	}

	[Test]
	public async Task ThrowOnUnmappedFailure_DefaultsToOff()
	{
		ResultsHttpOptions options = new();

		await Assert.That(options.ThrowOnUnmappedFailure).IsFalse();
	}

	[Test]
	public async Task Fallbacks_OnceRead_IgnoresLaterAdditions()
	{
		// Arrange — the options are startup configuration and the mapper reading them is a singleton, so
		// the list is frozen on first read rather than mutated while request threads enumerate it.
		ResultsHttpOptions options = new();
		options.AddFallback(static (_, _) => null);

		// Act
		var firstRead = options.Fallbacks;
		options.AddFallback(static (_, _) => null);
		var secondRead = options.Fallbacks;

		// Assert
		await Assert.That(firstRead.Count).IsEqualTo(1);
		await Assert.That(secondRead.Count).IsEqualTo(1);
		await Assert.That(ReferenceEquals(firstRead, secondRead)).IsTrue();
	}

	[Test]
	public async Task Fallbacks_ReadConcurrently_IsStable()
	{
		// Arrange
		ResultsHttpOptions options = new();
		options.AddFallback(static (_, _) => null);
		options.AddFallback(static (_, _) => null);

		// Act — enumerate from many threads at once. Against the previous plain List this is the shape that
		// produced "Collection was modified" when a fallback was added after startup.
		var counts = await Task.WhenAll(
			Enumerable.Range(0, 64).Select(_ => Task.Run(() => options.Fallbacks.Count(static f => f is not null)))
		);

		// Assert
		await Assert.That(counts.Distinct().Count()).IsEqualTo(1);
		await Assert.That(counts[0]).IsEqualTo(2);
	}
}
