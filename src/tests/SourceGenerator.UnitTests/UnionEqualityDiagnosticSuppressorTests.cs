using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Results.SourceGenerator.Suppressors;
using System.Collections.Immutable;
using System.Globalization;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Tests for <see cref="UnionEqualityDiagnosticSuppressor"/>, the component that keeps <c>CA1815</c> off
/// union declarations opted in with <c>[GenerateResult]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The real <c>CA1815</c> is reported by the .NET analyzers the SDK loads, which a unit-test compilation
/// cannot reference, so these tests report the same id at the same location
/// (<see cref="Ca1815ReporterAnalyzer"/>) and run it beside the suppressor through
/// <c>CompilationWithAnalyzers</c>. Everything else is real: the compilation is produced by running the
/// generator, so the union, the generated attribute and the generated helper class are all present, and the
/// suppressor under test is the shipped type.
/// </para>
/// <para>
/// The tests assert both halves of the contract: the suppressor removes <c>CA1815</c> for an opted-in union,
/// and leaves it alone for a union that did not opt in, for the union's case types, and for any other value
/// type.
/// </para>
/// </remarks>
public class UnionEqualityDiagnosticSuppressorTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	const string OptedInUnionSource = """
		namespace Test
		{
			public readonly record struct NotFound(int Id);

			[GenerateResult]
			public readonly union TenantError(NotFound);
		}
		""";

	const string UnionWithoutOptInSource = """
		namespace Test
		{
			public readonly record struct NotFound(int Id);

			public readonly union TenantError(NotFound);
		}
		""";

	[Test]
	public async Task SupportedSuppressions_GivenSuppressor_DeclaresOnlyTheCa1815Bucket()
	{
		// Arrange
		// Act
		UnionEqualityDiagnosticSuppressor suppressor = new();

		// Assert
		await Assert.That(suppressor.SupportedSuppressions.Length).IsEqualTo(1);

		var suppression = suppressor.SupportedSuppressions[0];

		// The suppression id is this component's own bucket (the rules it reports are RSG1000-RSG1007), and it
		// is the id a consumer adds to NoWarn to keep CA1815 instead.
		await Assert.That(suppression.Id).IsEqualTo("RSG2000");
		await Assert.That(suppression.SuppressedDiagnosticId).IsEqualTo("CA1815");
		await Assert.That(suppression.Justification.ToString(CultureInfo.InvariantCulture)).IsNotEmpty();
	}

	[Test]
	public async Task ReportSuppressions_GivenOptedInUnion_SuppressesCa1815OnTheUnionOnly(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var compilation = await GenerateCompilationAsync(OptedInUnionSource, cancellationToken);

		// The reporter must actually raise the warning, otherwise the suppression below would pass vacuously.
		var withoutSuppressor = await AnalyzeAsync(compilation, withSuppressor: false, cancellationToken);

		await Assert.That(Messages(withoutSuppressor, "TenantError")).IsEquivalentTo(["CA1815:suppressed=False"]);

		// Act
		var withSuppressor = await AnalyzeAsync(compilation, withSuppressor: true, cancellationToken);

		// Assert
		// The union's warning is reported and marked suppressed, while its case type - an ordinary value type -
		// keeps the warning it would get without this component.
		await Assert.That(Messages(withSuppressor, "TenantError")).IsEquivalentTo(["CA1815:suppressed=True"]);
		await Assert.That(Messages(withSuppressor, "NotFound")).IsEquivalentTo(["CA1815:suppressed=False"]);
	}

	[Test]
	public async Task ReportSuppressions_GivenUnionWithoutTheOptInAttribute_LeavesCa1815Unsuppressed(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var compilation = await GenerateCompilationAsync(UnionWithoutOptInSource, cancellationToken);

		// Act
		var withSuppressor = await AnalyzeAsync(compilation, withSuppressor: true, cancellationToken);

		// Assert
		// A union this component does not generate for is none of its business.
		await Assert.That(Messages(withSuppressor, "TenantError")).IsEquivalentTo(["CA1815:suppressed=False"]);
	}

	[Test]
	public async Task ReportSuppressions_GivenNonUnionValueType_LeavesCa1815Unsuppressed(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var compilation = await GenerateCompilationAsync(UnionWithoutOptInSource, cancellationToken);

		// Act
		var withSuppressor = await AnalyzeAsync(compilation, withSuppressor: true, cancellationToken);

		// Assert
		await Assert.That(Messages(withSuppressor, "NotFound")).IsEquivalentTo(["CA1815:suppressed=False"]);
	}

	/// <summary>
	/// Runs the generator so the compilation contains the generated attribute and helpers, and asserts it
	/// compiles.
	/// </summary>
	async Task<Compilation> GenerateCompilationAsync(string source, CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(source, cancellationToken);

		result.AssertNoCompilationErrors();

		return result.CompilationResult.Compilation;
	}

	/// <summary>
	/// Runs the CA1815 reporter - and, optionally, the suppressor - over a compilation and returns every
	/// diagnostic, including the suppressed ones.
	/// </summary>
	static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
		Compilation compilation,
		bool withSuppressor,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<DiagnosticAnalyzer> analyzers = withSuppressor
			? [new Ca1815ReporterAnalyzer(), new UnionEqualityDiagnosticSuppressor()]
			: [new Ca1815ReporterAnalyzer()];

		// ReportSuppressedDiagnostics is what makes the suppressor observable: a suppressed diagnostic is
		// returned with IsSuppressed set instead of being filtered out.
		CompilationWithAnalyzersOptions options = new(
			new AnalyzerOptions([]),
			onAnalyzerException: null,
			concurrentAnalysis: true,
			logAnalyzerExecutionTime: false,
			reportSuppressedDiagnostics: true,
			analyzerExceptionFilter: null
		);

		return await compilation.WithAnalyzers(analyzers, options).GetAllDiagnosticsAsync(cancellationToken);
	}

	/// <summary>
	/// Projects the CA1815 diagnostics the reporter raised for one type into an assertable shape, so a failure
	/// shows which instance kept or lost the warning.
	/// </summary>
	static string[] Messages(ImmutableArray<Diagnostic> diagnostics, string typeName) =>
		[
			.. diagnostics
				.Where(static diagnostic => diagnostic.Id == "CA1815")
				// The generated extension class is named after the union, but generated code is not analyzed, so
				// matching the quoted type name in the message keeps the type's own instance and nothing else.
				.Where(diagnostic =>
					diagnostic
						.GetMessage(CultureInfo.InvariantCulture)
						.Contains($"'{typeName}'", StringComparison.Ordinal)
				)
				.Select(static diagnostic => $"CA1815:suppressed={diagnostic.IsSuppressed}")
				.Order(StringComparer.Ordinal),
		];
}

/// <summary>
/// Reports <c>CA1815</c> at every named type declaration's location, standing in for the .NET analyzer that
/// owns the rule (see the test class remarks).
/// </summary>
// The stub deliberately reports the real rule's id, and it lives in a test assembly that references Workspaces
// and targets net11.0, which the compiler-extension authoring rules warn about.
#pragma warning disable RS1029, RS1036, RS1038, RS1041, RS2008
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ca1815ReporterAnalyzer : DiagnosticAnalyzer
{
	static readonly DiagnosticDescriptor Ca1815 = new(
		id: "CA1815",
		title: "Override equals and operator equals on value types",
		messageFormat: "'{0}' should override Equals",
		category: "Performance",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Ca1815];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(
			static symbolContext =>
			{
				var symbol = (INamedTypeSymbol)symbolContext.Symbol;

				if (symbol.Locations.IsDefaultOrEmpty)
					return;

				symbolContext.ReportDiagnostic(Diagnostic.Create(Ca1815, symbol.Locations[0], symbol.Name));
			},
			SymbolKind.NamedType
		);
	}
}
#pragma warning restore RS1029, RS1036, RS1038, RS1041, RS2008
