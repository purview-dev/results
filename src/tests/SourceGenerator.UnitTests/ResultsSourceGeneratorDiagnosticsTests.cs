using Purview.Results.SourceGeneration;
using Purview.Results.SourceGeneration.Analyzers;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Tests for the diagnostics the source generator owns: the compilation-wide rules it reports
/// (<c>RSG1005</c>/<c>RSG1006</c>) and the blocking behaviour driven by the shared diagnostics library.
/// </summary>
/// <remarks>
/// The per-target rules are reported by <see cref="ResultsDiagnosticAnalyzer"/> and asserted in
/// <see cref="ResultsDiagnosticAnalyzerTests"/>; the tests here prove the generator applies the same
/// blocking decisions without reporting the same rule twice.
/// </remarks>
public class ResultsSourceGeneratorDiagnosticsTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	[Test]
	public async Task GenerateAsync_GivenAttributeOnNonUnionTargets_EmitsNoHelpersAndReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		string[] targets =
		[
			"[GenerateResult] public sealed class NotAUnion;",
			"[GenerateResult] public readonly struct NotAUnion;",
			"[GenerateResult] public sealed record NotAUnion(int Id);",
			"[GenerateResult] public enum NotAUnion { None }",
			"[GenerateResult] public interface NotAUnion;",
		];

		foreach (var target in targets)
		{
			// Act
			var result = await GenerateAsync($"namespace Test {{ {target} }}", cancellationToken);

			// Assert
			// The analyzer reports RSG1000; the generator blocks generation and reports nothing itself.
			await Assert.That(result).HasNoDiagnostics();
			await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
		}
	}

	[Test]
	public async Task GenerateAsync_GivenGenericUnion_EmitsNoHelpersAndReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				[GenerateResult]
				public readonly union BoxError<T>(T, string);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
		await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
	}

	[Test]
	public async Task GenerateAsync_GivenUnionWithNoCases_EmitsNoHelpersAndReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				[GenerateResult]
				public readonly union EmptyError();
			}
			""";

		// Act
		var result = await GenerateAsync(
			source,
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
		await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
	}

	[Test]
	public async Task GenerateAsync_GivenInaccessibleCaseType_EmitsHelpersForTheRemainingCases(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				file readonly record struct HiddenCase(int Id);

				public readonly record struct VisibleCase(int Id);

				[GenerateResult]
				public readonly union MixedVisibilityError(HiddenCase, VisibleCase);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		// RSG1003 is a case-scope finding reported by the analyzer, and generation continues for the
		// remaining cases.
		await Assert.That(result).HasNoDiagnostics();

		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("this global::Test.VisibleCase error");
		await Assert.That(generated).DoesNotContain("HiddenCase");
	}

	[Test]
	public async Task GenerateAsync_GivenUnionConfiguredTwice_GeneratesTheHelpersOnceAndReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string first = """
			namespace Test
			{
				public readonly record struct NotFound(int Id);

				[GenerateResult]
				public readonly partial union TenantError(NotFound);
			}
			""";
		const string second = """
			namespace Test
			{
				[GenerateResult]
				public readonly partial union TenantError;
			}
			""";

		// Act
		var result = await GenerateAsync(
			[first, second],
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		// Assert
		// RSG1004 is reported by the analyzer and is non-blocking: the union is valid and generated once.
		await Assert.That(result).HasNoDiagnostics();
		result.AssertGeneratedSourceCount(1);
	}

	[Test]
	public async Task GenerateAsync_GivenSharedCaseType_ReportsSharedCaseTypeAndGeneratesTheHelperOnce(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct NotFound(int Id);

				public readonly record struct Disabled(int Id);

				[GenerateResult]
				public readonly union TenantError(NotFound, Disabled);

				[GenerateResult]
				public readonly union AuditError(NotFound);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		// RSG1006 needs every opted-in union, so the generator reports it; it is non-blocking and only the
		// shared case's helper is skipped.
		await Assert.That(result).HasDiagnostic("RSG1006");

		var auditTree = result.PrimarySyntaxTrees.Single(static tree =>
			tree.FilePath.EndsWith("AuditErrorResultExtensions.g.cs", StringComparison.Ordinal)
		);
		var tenantTree = result.PrimarySyntaxTrees.Single(static tree =>
			tree.FilePath.EndsWith("TenantErrorResultExtensions.g.cs", StringComparison.Ordinal)
		);
		var auditSource = (await auditTree.GetTextAsync(cancellationToken)).ToString();
		var tenantSource = (await tenantTree.GetTextAsync(cancellationToken)).ToString();

		await Assert.That(auditSource).Contains("this global::Test.NotFound error");
		await Assert.That(tenantSource).Contains("this global::Test.Disabled error");
		await Assert.That(tenantSource).DoesNotContain("this global::Test.NotFound error");
	}

	[Test]
	public async Task GenerateAsync_GivenCollidingGeneratedClassNames_ReportsGeneratedClassNameCollision(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// A top-level union named 'Outer_TenantError' produces the same generated class name
		// ('Outer_TenantErrorResultExtensions') as the union 'TenantError' nested in 'Outer'.
		const string source = """
			namespace Test
			{
				public readonly record struct NestedCase(int Id);

				public readonly record struct TopLevelCase(int Id);

				public static class Outer
				{
					[GenerateResult]
					public readonly union TenantError(NestedCase);
				}

				[GenerateResult]
				public readonly union Outer_TenantError(TopLevelCase);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1005");

		// RSG1005 is blocking for the colliding union, so only the deterministic winner is generated.
		result.AssertGeneratedSourceCount(1);
		await Assert.That(result).HasGeneratedSyntaxTree("Test.Outer_TenantErrorResultExtensions.g.cs");
	}

	[Test]
	public async Task GenerateAsync_GivenAnalyzerAlongsideTheGenerator_ReportsEachRuleOnce(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				[GenerateResult]
				public sealed class NotAUnion;
			}
			""";

		// Act
		var result = await GenerateAsync(
			source,
			new ResultsSourceGeneratorTestOptions
			{
				AnalyzerTypes = [typeof(ResultsDiagnosticAnalyzer)],
				ThrowOnGenerationException = false,
			},
			cancellationToken
		);

		// Assert
		// Both hosts consult the shared diagnostics library: the analyzer raises the per-target rule while
		// the generator only uses it to block generation, so the rule is never reported twice.
		await Assert.That(result.AnalyzerResult).IsNotNull();

		var analyzerDiagnostics = result
			.AnalyzerResult!.Diagnostics.Select(static diagnostic => diagnostic.Id)
			.ToArray();
		await Assert.That(analyzerDiagnostics).Contains("RSG1000");

		// The generator itself reports nothing for this target, which is what keeps the rule from appearing
		// twice in build output.
		await Assert.That(result.DriverResult.Diagnostics).IsEmpty();
		await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
	}
}
