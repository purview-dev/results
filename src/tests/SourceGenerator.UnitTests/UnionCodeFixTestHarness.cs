using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Purview.Results.SourceGenerator.CodeFixes;
using System.Collections.Immutable;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Applies <see cref="UnionCaseResultCodeFixProvider"/> the way an IDE would.
/// </summary>
/// <remarks>
/// <para>
/// The framework's code-fix test base is driven by an analyzer's diagnostics, but this fix answers a compiler
/// diagnostic (<c>CS0029</c>), so the harness composes the compilation itself: it parses the source, runs the
/// generator (the generated <c>[GenerateResult]</c> attribute and the <c>AsFailure</c> helpers must exist for
/// the rewrite to bind), and applies the offered fix through an <see cref="AdhocWorkspace"/>.
/// </para>
/// <para>
/// The workspace project receives the user document <em>and</em> every generated document, and the document
/// handed to the provider is re-resolved from the workspace's current solution: a <see cref="Document"/> is an
/// immutable snapshot, so the instance captured before the generated documents were added would resolve a
/// compilation without them.
/// </para>
/// </remarks>
public static class UnionCodeFixTestHarness
{
	/// <summary>
	/// Applies the first offered code fix to <paramref name="source"/>.
	/// </summary>
	/// <param name="source">The user source that contains the failing conversion.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>The harness outcome.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "Test harness: composing a Roslyn compilation, an AdhocWorkspace, a generator driver and a code fix necessarily touches many Roslyn types."
	)]
	public static async Task<CodeFixHarnessResult> ApplyAsync(string source, CancellationToken cancellationToken)
	{
		CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
		CSharpCompilationOptions compilationOptions = new(
			OutputKind.DynamicallyLinkedLibrary,
			nullableContextOptions: NullableContextOptions.Enable
		);

		var tree = CSharpSyntaxTree.ParseText(
			source,
			parseOptions,
			path: "Test.cs",
			cancellationToken: cancellationToken
		);
		var references = UnionCodeFixTestCompilation.MetadataReferences();

		var compilation = CSharpCompilation.Create("CodeFixTest", [tree], references, compilationOptions);

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[new ResultsSourceGenerator().AsSourceGenerator()],
			parseOptions: parseOptions
		);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _, cancellationToken);

		var generatedTrees = updated.SyntaxTrees.Where(generated => generated != tree).ToArray();

		UnionCaseResultCodeFixProvider provider = new();

		var fixable = updated
			.GetDiagnostics(cancellationToken)
			.Where(diagnostic =>
				diagnostic.Severity == DiagnosticSeverity.Error
				&& diagnostic.Location.SourceTree == tree
				&& provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal)
			)
			.ToArray();

		if (fixable.Length == 0)
			return new CodeFixHarnessResult(FixOffered: false, FixedCode: null, RemainingErrorIds: []);

		using AdhocWorkspace workspace = new();

		var projectId = ProjectId.CreateNewId();
		workspace.AddProject(
			ProjectInfo.Create(
				projectId,
				VersionStamp.Create(),
				"CodeFixTest",
				"CodeFixTest",
				LanguageNames.CSharp,
				parseOptions: parseOptions,
				compilationOptions: compilationOptions,
				metadataReferences: references
			)
		);

		var documentId = workspace.AddDocument(projectId, "Test.cs", await tree.GetTextAsync(cancellationToken)).Id;

		foreach (var generated in generatedTrees)
		{
			var name = string.IsNullOrEmpty(generated.FilePath) ? "Generated.g.cs" : generated.FilePath;
			workspace.AddDocument(projectId, name, await generated.GetTextAsync(cancellationToken));
		}

		// Re-resolve: the Document created above predates the generated documents, so its project would
		// resolve a compilation without the generated attribute and helpers.
		var document = workspace.CurrentSolution.GetDocument(documentId)!;
		var documentTree = await document.GetSyntaxTreeAsync(cancellationToken);

		var diagnostic = Diagnostic.Create(
			fixable[0].Descriptor,
			documentTree!.GetLocation(fixable[0].Location.SourceSpan)
		);

		List<CodeAction> actions = [];
		await provider.RegisterCodeFixesAsync(
			new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken)
		);

		if (actions.Count == 0)
			return new CodeFixHarnessResult(FixOffered: false, FixedCode: null, RemainingErrorIds: []);

		var operations = await actions[0].GetOperationsAsync(cancellationToken);
		var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
		var fixedDocument = await Formatter.FormatAsync(
			changedSolution.GetDocument(documentId)!,
			cancellationToken: cancellationToken
		);

		var fixedCode = (await fixedDocument.GetTextAsync(cancellationToken)).ToString();

		// Recompile the rewrite with the generated sources to prove it actually binds.
		var fixedTree = CSharpSyntaxTree.ParseText(
			fixedCode,
			parseOptions,
			path: "Test.cs",
			cancellationToken: cancellationToken
		);
		var recompiled = CSharpCompilation.Create(
			"CodeFixTest",
			[fixedTree, .. generatedTrees],
			references,
			compilationOptions
		);

		return new CodeFixHarnessResult(
			FixOffered: true,
			FixedCode: fixedCode,
			RemainingErrorIds:
			[
				.. recompiled
					.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(static diagnostic => diagnostic.Id)
					.Distinct(StringComparer.Ordinal)
					.Order(StringComparer.Ordinal),
			]
		);
	}
}

/// <summary>
/// The metadata references the code-fix test compilation is built from.
/// </summary>
static class UnionCodeFixTestCompilation
{
	/// <summary>
	/// Creates the references: the test host's platform assemblies plus the runtime results assembly the
	/// generated helpers target.
	/// </summary>
	public static ImmutableArray<MetadataReference> MetadataReferences()
	{
		var builder = ImmutableArray.CreateBuilder<MetadataReference>();

		var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty).Split(
			Path.PathSeparator,
			StringSplitOptions.RemoveEmptyEntries
		);

		foreach (var path in trusted)
			builder.Add(MetadataReference.CreateFromFile(path));

		builder.Add(MetadataReference.CreateFromFile(typeof(Result<,>).Assembly.Location));

		return builder.ToImmutable();
	}
}

/// <summary>
/// The outcome of applying a code fix: whether one was offered, the fixed source, and the compiler errors the
/// rewrite still produces.
/// </summary>
/// <param name="FixOffered">Whether the provider registered a fix.</param>
/// <param name="FixedCode">The rewritten source, or <see langword="null"/> when no fix was offered.</param>
/// <param name="RemainingErrorIds">The distinct compiler error ids the rewritten source produces.</param>
public sealed record CodeFixHarnessResult(bool FixOffered, string? FixedCode, ImmutableArray<string> RemainingErrorIds)
{
	/// <summary>
	/// Gets a value indicating whether the rewrite compiles cleanly against the generated sources.
	/// </summary>
	public bool RewriteCompiles => RemainingErrorIds.IsEmpty;
}
