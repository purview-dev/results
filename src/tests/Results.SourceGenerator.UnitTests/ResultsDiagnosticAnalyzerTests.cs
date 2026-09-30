using System.Globalization;
using Microsoft.CodeAnalysis;
using Purview.Results.SourceGeneration.Analyzers;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Tests for <see cref="ResultsDiagnosticAnalyzer"/>, the reporting host for the per-target rules of the
/// shared diagnostics library.
/// </summary>
public class ResultsDiagnosticAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<ResultsDiagnosticAnalyzer, ResultsSourceGeneratorTestOptions>
{
	[Test]
	public async Task SupportedDiagnostics_GivenAnalyzer_IsTheWholeSharedCatalogue()
	{
		// Arrange
		// Act
		ResultsDiagnosticAnalyzer analyzer = new();

		// Assert
		// The analyzer and the generator share one catalogue, so the analyzer must support every rule.
		await Assert
			.That(
				analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id).Order(StringComparer.Ordinal)
			)
			.IsEquivalentTo(["RSG1000", "RSG1001", "RSG1002", "RSG1003", "RSG1004", "RSG1005", "RSG1006", "RSG1007"]);
	}

	[Test]
	public async Task AnalyzeAsync_GivenAttributeOnNonUnionTargets_ReportsTargetIsNotUnion(
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
			var result = await AnalyzeAsync(
				[TestSources.GenerateResultAttributeDeclaration, $"namespace Test {{ {target} }}"],
				cancellationToken
			);

			// Assert
			await Assert.That(result).HasDiagnostic("RSG1000");

			var diagnostic = result.Diagnostics.Single(static candidate => candidate.Id == "RSG1000");
			await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
			await Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture)).Contains("NotAUnion");
		}
	}

	[Test]
	public async Task AnalyzeAsync_GivenUnionWithNoCases_ReportsUnionHasNoCases(CancellationToken cancellationToken)
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
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1001");
	}

	[Test]
	public async Task AnalyzeAsync_GivenGenericUnion_ReportsUnsupportedGenericConfiguration(
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
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1002");
	}

	[Test]
	public async Task AnalyzeAsync_GivenInaccessibleCaseType_ReportsInaccessibleCaseType(
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
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1003");

		var diagnostic = result.Diagnostics.Single(static candidate => candidate.Id == "RSG1003");
		await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
	}

	[Test]
	public async Task AnalyzeAsync_GivenUnionConfiguredTwice_ReportsDuplicateConfiguration(
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
		var result = await AnalyzeAsync(
			[TestSources.GenerateResultAttributeDeclaration, first, second],
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1004");
	}

	[Test]
	public async Task AnalyzeAsync_GivenUnionMemberProvider_ReportsUnsupportedMemberProvider(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct ProvidedCase(int Id);

				[GenerateResult]
				[System.Runtime.CompilerServices.Union]
				public readonly struct ProvidedError : System.Runtime.CompilerServices.IUnion
				{
					public interface IUnionMembers
					{
						ProvidedCase Value { get; }
					}

					readonly object? _value;

					public ProvidedError(ProvidedCase value) => _value = value;

					object? System.Runtime.CompilerServices.IUnion.Value => _value;
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic("RSG1007");
	}

	[Test]
	public async Task AnalyzeAsync_GivenValidOptedInUnion_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct NotFound(int Id);

				[GenerateResult]
				public readonly union TenantError(NotFound);
			}
			""";

		// Act
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task AnalyzeAsync_GivenSharedCaseType_DoesNotReportTheCompilationWideRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// RSG1005 and RSG1006 need every opted-in union in the compilation, so the generator reports them;
		// the analyzer must not report the same rule from a single symbol.
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
		var result = await AnalyzeAsync([TestSources.GenerateResultAttributeDeclaration, source], cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic("RSG1005");
		await Assert.That(result).DoesNotHaveDiagnostic("RSG1006");
	}
}
