namespace Purview.Results.SourceGenerator;

/// <summary>
/// Guards the package assets that make the generator's build switches reachable by <c>PackageReference</c>
/// consumers.
/// </summary>
/// <remarks>
/// A property declared only inside this repository never reaches a consumer's compiler, so the documented
/// <c>ResultsSourceGenerator_Disable</c> switch silently does nothing unless the package ships it as a
/// <c>buildTransitive/*.props</c> asset declaring a <c>CompilerVisibleProperty</c>. These tests fail if that
/// asset is deleted, renamed, or drifts away from the property the project declares.
/// </remarks>
public sealed class SourceGeneratorPackageAssetsTests
{
	const string DisablePropertyName = "ResultsSourceGenerator_Disable";
	const string PackagedPropsPath =
		"src/src/SourceGenerator/Sdk/buildTransitive/Purview.Results.SourceGenerator.props";
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
