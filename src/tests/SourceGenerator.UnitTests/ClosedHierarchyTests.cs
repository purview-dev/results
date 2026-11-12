// CSharpier 1.4.0 parses the C# 15 'closed' modifier, so this file needs no formatter exclusion.

namespace Purview.Results.SourceGenerator;

/// <summary>
/// A closed error hierarchy. <c>closed</c> fixes the direct descendants at compile time, so a switch over them
/// is exhaustive without a discard arm.
/// </summary>
/// <remarks>
/// A union remains the better fit for data-only error cases, because every case is declared in one place. A
/// closed hierarchy is the right tool when the cases carry shared behaviour or need inheritance; either shape
/// works as <c>TError</c>.
/// </remarks>
public closed record class SyncOutcome;

/// <summary>The sync completed.</summary>
/// <param name="Count">The number of records synchronized.</param>
public record class Synced(int Count) : SyncOutcome;

/// <summary>The sync did not run.</summary>
/// <param name="Reason">The reason the sync was skipped.</param>
public record class Skipped(string Reason) : SyncOutcome;

/// <summary>
/// Proves a C# 15 closed hierarchy composes with <see cref="Result{TValue, TError}"/> and that the compiler
/// treats a switch over its direct descendants as exhaustive.
/// </summary>
public class ClosedHierarchyTests
{
	[Test]
	public async Task Match_GivenClosedErrorHierarchy_ExhaustivelySwitchesWithoutDefaultArm()
	{
		// Arrange
		Result<int, SyncOutcome> result = new Skipped("offline");

		// Act
		var description = result.Match(value => $"synced {value}", Describe);

		// Assert — warnings are errors in this repository, so a missing direct descendant would fail the build.
		await Assert.That(description).IsEqualTo("skipped: offline");
	}

	// No discard arm: adding a new direct descendant of SyncOutcome breaks this switch at compile time, which
	// is the exhaustiveness guarantee 'closed' provides.
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static string Describe(SyncOutcome outcome) =>
		outcome switch
		{
			Synced synced => $"synced {synced.Count}",
			Skipped skipped => $"skipped: {skipped.Reason}",
		};
}
