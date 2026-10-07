namespace Purview.Results.SourceGenerator;

/// <summary>
/// Guards the package assets that make the generator's build switches reachable by <c>PackageReference</c>
/// consumers.
/// </summary>
/// <remarks>
/// The generator ships inside the <c>Purview.Results</c> package, so its build switches are reachable only
/// when that package ships a <c>buildTransitive/*.props</c> asset declaring a <c>CompilerVisibleProperty</c>.
/// A property declared only inside this repository never reaches a consumer's compiler, so the documented
/// <c>DisableResultsSourceGenerator</c> switch silently does nothing without that asset. These tests fail if
/// the asset is deleted, renamed, or drifts away from the property the project declares.
/// </remarks>
public sealed class SourceGeneratorPackageAssetsTests
{
	const string DisablePropertyName = "DisableResultsSourceGenerator";
	const string PackagedPropsPath = "src/src/Results/Sdk/buildTransitive/Purview.Results.props";
	const string ResultsProjectPath = "src/src/Results/Results.csproj";
	const string ProjectPath = "src/src/SourceGenerator/SourceGenerator.csproj";

	[Test]
	public async Task PackagedBuildProps_ShouldDeclareTheGeneratorDisableProperty()
	{
		var path = Path.Combine(RepositoryRoot(), PackagedPropsPath);

		await Assert.That(File.Exists(path)).IsTrue();

		var content = await File.ReadAllTextAsync(path);

		await Assert.That(content).Contains($"<CompilerVisibleProperty Include=\"{DisablePropertyName}\">");
	}

	[Test]
	public async Task PackagedBuildProps_ShouldDeclareThePropertyTheProjectReads()
	{
		var project = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ProjectPath));

		// The project names the property it reads through ResultsSourceGeneratorDisableProperty, and the
		// packaged asset must expose exactly that name to consumers.
		await Assert
			.That(project)
			.Contains(
				$"<ResultsSourceGeneratorDisableProperty>{DisablePropertyName}</ResultsSourceGeneratorDisableProperty>"
			);

		var content = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), PackagedPropsPath));

		await Assert.That(content).Contains(DisablePropertyName);
	}

	[Test]
	public async Task ResultsProject_ShouldBundleTheMergedGeneratorAndCodeFix()
	{
		// The generator is retired as its own package, so Purview.Results must pack the merged analyzer and
		// the code fix under analyzers/dotnet/cs without running the generator on this library's compilation.
		var project = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ResultsProjectPath));

		await Assert.That(project).Contains("PackResultsSourceGenerator");
		await Assert.That(project).Contains("GetPurviewMergedAnalyzerFile");
		await Assert.That(project).Contains("..\\SourceGenerator.CodeFixes\\SourceGenerator.CodeFixes.csproj");
		await Assert.That(project).Contains("analyzers/dotnet/cs/");
	}

	[Test]
	public async Task SourceGeneratorProject_ShouldNotBePackable()
	{
		var project = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), ProjectPath));

		await Assert.That(project).DoesNotContain("<IsPackable>true</IsPackable>");
	}

	static string RepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "src", "Results.slnx")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			$"The repository root (the directory containing src/Results.slnx) was not found above '{AppContext.BaseDirectory}'."
		);
	}
}
