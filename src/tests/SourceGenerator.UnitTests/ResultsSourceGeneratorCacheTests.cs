using Microsoft.CodeAnalysis;
using Purview.Results.SourceGeneration;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Incremental-correctness tests for <see cref="ResultsSourceGenerator"/>: they assert which pipeline
/// stages are recomputed and which are reused, rather than only comparing generated text.
/// </summary>
public class ResultsSourceGeneratorCacheTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	const string TenantUnion = """
		namespace Test
		{
			public readonly record struct TenantNotFound(int TenantId);

			[GenerateResult]
			public readonly union TenantError(TenantNotFound);
		}
		""";

	const string TenantUnionWithExtraCase = """
		namespace Test
		{
			public readonly record struct TenantNotFound(int TenantId);

			public readonly record struct TenantDisabled(int TenantId);

			[GenerateResult]
			public readonly union TenantError(TenantNotFound, TenantDisabled);
		}
		""";

	const string RepositoryUnion = """
		namespace Test
		{
			public readonly record struct RepositoryNotFound(int RepositoryId);

			[GenerateResult]
			public readonly union RepositoryError(RepositoryNotFound);
		}
		""";

	const string UnrelatedSource = """
		namespace Test
		{
			public sealed class Unrelated;
		}
		""";

	[Test]
	public async Task GenerateIncrementalAsync_GivenFirstRun_MarksEveryStageNew(CancellationToken cancellationToken)
	{
		// Arrange
		// Act
		var result = await GenerateIncrementalAsync(
			[new IncrementalRunInput([TenantUnion])],
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result.Runs[0]).AllStepsNew();
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenIdenticalRerun_ReusesTheModelStage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Act
		var result = await GenerateIncrementalAsync([TenantUnion], cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result.Runs[1]).StepIsCached("GetResultUnionGenerationModel");
		await Assert
			.That(result.Runs[1])
			.HasStepReason("ForAttribute_GenerateResultAttribute", IncrementalStepRunReason.Unchanged);
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenIdenticalRerun_ReusesEveryStageTheGeneratorOwns(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Act
		var result = await GenerateIncrementalAsync([TenantUnion], cancellationToken: cancellationToken);

		// Assert
		// The stages the generator owns are all reused, which is the cache requirement: a rerun must not
		// re-read configuration, re-run discovery or re-generate source.
		await Assert.That(result.Runs[1]).StepIsCached("GetGenerationContext_EmptyCapabilities");
		await Assert.That(result.Runs[1]).StepIsCached("ForAttribute_GenerateResultAttribute");
		await Assert.That(result.Runs[1]).StepIsCached("GetResultUnionGenerationModel");
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenDisablePropertySet_InvalidatesTheConfigurationStageOnly(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var inputs = new[]
		{
			new IncrementalRunInput([TenantUnion]),
			new IncrementalRunInput(
				[TenantUnion],
				[($"build_property.{ResultsSourceGeneratorTestOptions.DisableGeneratorProperty}", "true")]
			),
		};

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		// Changing one analyzer-config property invalidates the configuration-dependent stage...
		await Assert
			.That(result.Runs[1])
			.HasStepReason("GetGenerationContext_EmptyCapabilities", IncrementalStepRunReason.Modified);

		// ...and the disabled generator produces no helper sources at all.
		await Assert.That(GeneratedHelperHintNames(result.Runs[1])).IsEmpty();
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenUnrelatedSourceEdit_LeavesTheModelStageUnchanged(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var inputs = new[]
		{
			new IncrementalRunInput([TenantUnion]),
			new IncrementalRunInput([TenantUnion, UnrelatedSource]),
		};

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		// The compilation changed, so the attribute stage re-runs, but the union model is value-equal:
		// the model stage is cached and no source is regenerated.
		await Assert.That(result.Runs[1]).StepIsCached("GetResultUnionGenerationModel");
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenUnionEdit_ModifiesTheModelAndTheOutput(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var inputs = new[]
		{
			new IncrementalRunInput([TenantUnion]),
			new IncrementalRunInput([TenantUnionWithExtraCase]),
		};

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		await Assert
			.That(result.Runs[1])
			.HasStepReason("GetResultUnionGenerationModel", IncrementalStepRunReason.Modified);

		var generated = GeneratedHelperHintNames(result.Runs[1]);
		await Assert.That(generated).IsEquivalentTo(["Test.TenantErrorResultExtensions.g.cs"]);
	}

	[Test]
	public async Task GenerateIncrementalAsync_GivenSecondUnionAdded_AddsOnlyItsOwnOutput(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var inputs = new[]
		{
			new IncrementalRunInput([TenantUnion]),
			new IncrementalRunInput([TenantUnion, RepositoryUnion]),
		};

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		await Assert
			.That(result.Runs[1])
			.HasStepReason("GetResultUnionGenerationModel", IncrementalStepRunReason.Modified);

		var generated = GeneratedHelperHintNames(result.Runs[1]);
		await Assert
			.That(generated)
			.IsEquivalentTo(["Test.RepositoryErrorResultExtensions.g.cs", "Test.TenantErrorResultExtensions.g.cs"]);
	}

	/// <summary>
	/// Gets the hint names of the generated helper sources, excluding the sources emitted during
	/// post-initialization so only per-union output is asserted.
	/// </summary>
	static string[] GeneratedHelperHintNames(IncrementalCacheRun run) =>
		[
			.. run
				.RunResult.GeneratedSources.Select(static source => source.HintName)
				.Where(static hintName =>
					!ResultsSourceGeneratorTestOptions.PostInitializationHintNames.Contains(hintName)
				)
				.Order(StringComparer.Ordinal),
		];
}
