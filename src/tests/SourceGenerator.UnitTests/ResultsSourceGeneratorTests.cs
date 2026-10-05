namespace Purview.Results.SourceGenerator;

/// <summary>
/// Generation tests for <see cref="ResultsSourceGenerator"/>.
/// </summary>
public class ResultsSourceGeneratorTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	const string TenantUnionSource = """
		namespace Test
		{
			public readonly record struct TenantId(string Value);

			public readonly record struct Tenant(TenantId TenantId);

			public readonly record struct TenantNotFound(TenantId TenantId);

			public readonly record struct TenantDisabled(TenantId TenantId);

			public readonly record struct TenantAlreadyExists(TenantId TenantId);

			[GenerateResult]
			public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
		}
		""";

	[Test]
	public async Task GenerateAsync_GivenOptedInUnion_EmitsFailureHelpers(CancellationToken cancellationToken)
	{
		// Arrange
		// Act
		var result = await GenerateAsync(TenantUnionSource, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		result.AssertGeneratedSourceCount(1);
		await Assert.That(result).HasGeneratedSyntaxTree("Test.TenantErrorResultExtensions.g.cs");
		await Assert.That(result.Generated()).HasGeneratedClass("TenantErrorResultExtensions");
	}

	[Test]
	public async Task GenerateAsync_GivenOptedInUnion_EmitsOneHelperPerCase(CancellationToken cancellationToken)
	{
		// Arrange
		// Act
		var result = await GenerateAsync(TenantUnionSource, cancellationToken);

		// Assert
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("AsFailure");
		await Assert.That(generated).Contains("this global::Test.TenantNotFound error");
		await Assert.That(generated).Contains("this global::Test.TenantDisabled error");
		await Assert.That(generated).Contains("this global::Test.TenantAlreadyExists error");
		await Assert.That(generated).Contains("global::Purview.Results.Result<TValue, global::Test.TenantError>");
	}

	[Test]
	public async Task GenerateAsync_GivenOptedInUnion_EmitsAUnitFailureHelperPerCase(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Act
		var result = await GenerateAsync(TenantUnionSource, cancellationToken);

		// Assert
		var generated = result.AssertSingleGeneratedSource();

		// The unit helper's body names Result<TUnion> with no value type argument, which distinguishes it from
		// the generic AsFailure<TValue>() helper's Result<TValue, TUnion> body.
		await Assert
			.That(generated)
			.Contains("global::Purview.Results.Result<global::Test.TenantError>.Failure(error)");

		// The generic value helper is still emitted alongside it.
		await Assert
			.That(generated)
			.Contains("global::Purview.Results.Result<TValue, global::Test.TenantError>.Failure(error)");
	}

	[Test]
	public async Task GenerateAsync_GivenOptedInUnion_EmitsNullableEnabledFullyQualifiedSource(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Act
		var result = await GenerateAsync(TenantUnionSource, cancellationToken);

		// Assert
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("#nullable enable");
		await Assert.That(generated).Contains("namespace Test;");
		await Assert.That(generated).DoesNotContain("using ");
		await Assert.That(generated).Contains("Result<TValue, global::Test.TenantError>.Failure(error)");
	}

	[Test]
	public async Task GenerateAsync_GivenMultipleUnions_EmitsIndependentHelpersInDeterministicOrder(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct TenantNotFound(int TenantId);

				public readonly record struct RepositoryNotFound(int RepositoryId);

				public readonly record struct ObservationMissing(int ObservationId);

				[GenerateResult]
				public readonly union TenantError(TenantNotFound);

				[GenerateResult]
				public readonly union RepositoryError(RepositoryNotFound);

				[GenerateResult]
				public readonly union ObservationError(ObservationMissing);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		result.AssertGeneratedSourceCount(3);

		var hintNames = result.PrimarySyntaxTrees.Select(static tree => Path.GetFileName(tree.FilePath)).ToArray();
		await Assert
			.That(hintNames)
			.IsEquivalentTo([
				"Test.ObservationErrorResultExtensions.g.cs",
				"Test.RepositoryErrorResultExtensions.g.cs",
				"Test.TenantErrorResultExtensions.g.cs",
			]);

		var tenantTree = result.PrimarySyntaxTrees.Single(static tree =>
			tree.FilePath.EndsWith("TenantErrorResultExtensions.g.cs", StringComparison.Ordinal)
		);
		var tenantSource = (await tenantTree.GetTextAsync(cancellationToken)).ToString();

		await Assert.That(tenantSource).Contains("this global::Test.TenantNotFound error");
		await Assert.That(tenantSource).DoesNotContain("RepositoryNotFound");
		await Assert.That(tenantSource).DoesNotContain("ObservationMissing");
	}

	[Test]
	public async Task GenerateAsync_GivenGlobalNamespaceUnion_EmitsHelpersWithoutANamespace(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			public readonly record struct GlobalNotFound(int Id);

			[GenerateResult]
			public readonly union GlobalError(GlobalNotFound);
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		result.AssertGeneratedSourceCount(1);
		await Assert.That(result).HasGeneratedSyntaxTree("GlobalErrorResultExtensions.g.cs");

		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).DoesNotContain("namespace ");

		// A type in the global namespace is rendered without a global:: qualifier, which is what resolves
		// in the namespace-less generated file.
		await Assert.That(generated).Contains("this GlobalNotFound error");
	}

	[Test]
	public async Task GenerateAsync_GivenNestedNamespaceUnion_EmitsHelpersInThatNamespace(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test.Tenancy.Errors
			{
				public readonly record struct TenantNotFound(int TenantId);

				[GenerateResult]
				public readonly union TenantError(TenantNotFound);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		await Assert.That(result).HasGeneratedSyntaxTree("Test.Tenancy.Errors.TenantErrorResultExtensions.g.cs");

		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("namespace Test.Tenancy.Errors;");
		await Assert.That(generated).Contains("this global::Test.Tenancy.Errors.TenantNotFound error");
	}

	[Test]
	public async Task GenerateAsync_GivenSameShortCaseNamesInDifferentNamespaces_GeneratesBothWithoutAmbiguity(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Foo.Errors
			{
				public readonly record struct NotFound(int Id);

				[GenerateResult]
				public readonly union FooError(NotFound);
			}

			namespace Bar.Errors
			{
				public readonly record struct NotFound(int Id);

				[GenerateResult]
				public readonly union BarError(NotFound);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		result.AssertGeneratedSourceCount(2);

		var fooTree = result.PrimarySyntaxTrees.Single(static tree =>
			tree.FilePath.EndsWith("FooErrorResultExtensions.g.cs", StringComparison.Ordinal)
		);
		var fooSource = (await fooTree.GetTextAsync(cancellationToken)).ToString();

		await Assert.That(fooSource).Contains("this global::Foo.Errors.NotFound error");
		await Assert.That(fooSource).Contains("Result<TValue, global::Foo.Errors.FooError>");
		await Assert.That(fooSource).DoesNotContain("Bar.Errors");
	}

	[Test]
	public async Task GenerateAsync_GivenInternalUnionAndCases_EmitsInternalHelpers(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				internal readonly record struct InternalCase(int Id);

				[GenerateResult]
				internal readonly union InternalError(InternalCase);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("internal static class InternalErrorResultExtensions");
		await Assert.That(generated).DoesNotContain("public static class");
	}

	[Test]
	public async Task GenerateAsync_GivenPublicUnionWithInternalCase_EmitsInternalHelpers(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				internal readonly record struct InternalCase(int Id);

				[GenerateResult]
				public readonly union SemiPublicError(InternalCase);
			}
			""";

		// Act
		var result = await GenerateAsync(
			source,
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		// Assert
		// The union's own public constructor already reports CS0051 for the internal case type; the
		// generated helper class must still never be more accessible than the case it references.
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("internal static class SemiPublicErrorResultExtensions");
	}

	[Test]
	public async Task GenerateAsync_GivenEveryCaseKind_EmitsHelpersForEachKind(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct RecordStructCase(int Id);

				public readonly struct StructCase
				{
					public StructCase(int id) => Id = id;

					public int Id { get; }
				}

				public record class RecordClassCase(int Id);

				public class ClassCase
				{
					public ClassCase(int id) => Id = id;

					public int Id { get; }
				}

				[GenerateResult]
				public readonly union MixedCaseError(RecordStructCase, StructCase, RecordClassCase, ClassCase);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("this global::Test.RecordStructCase error");
		await Assert.That(generated).Contains("this global::Test.StructCase error");
		await Assert.That(generated).Contains("this global::Test.RecordClassCase error");
		await Assert.That(generated).Contains("this global::Test.ClassCase error");

		// The case types are non-nullable, so the generated parameters must not be annotated.
		await Assert.That(generated).DoesNotContain("RecordClassCase? error");
		await Assert.That(generated).DoesNotContain("ClassCase? error");
	}

	[Test]
	public async Task GenerateAsync_GivenConstructedGenericCaseType_EmitsHelperForTheConstructedType(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				[GenerateResult]
				public readonly union ItemsError(System.Collections.Generic.List<string>);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("this global::System.Collections.Generic.List<string> error");

		// The documentation text escapes the constructed generic type so the generated XML stays valid.
		await Assert.That(generated).Contains("List&lt;string&gt;");
	}

	[Test]
	public async Task GenerateAsync_GivenUnionNestedInAContainingType_EmitsHelpersWithADisambiguatedClassName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct NestedCase(int Id);

				public static class Container
				{
					[GenerateResult]
					public readonly union NestedError(NestedCase);
				}
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		await Assert.That(result).HasGeneratedSyntaxTree("Test.Container_NestedErrorResultExtensions.g.cs");

		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("class Container_NestedErrorResultExtensions");
		await Assert.That(generated).Contains("this global::Test.NestedCase error");
		await Assert.That(generated).Contains("Result<TValue, global::Test.Container.NestedError>");
	}

	[Test]
	public async Task GenerateAsync_GivenNestedCaseType_EmitsHelperForTheNestedType(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public static class Container
				{
					public readonly record struct NestedCase(int Id);
				}

				[GenerateResult]
				public readonly union NestedCaseError(Container.NestedCase);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();
		var generated = result.AssertSingleGeneratedSource();
		await Assert.That(generated).Contains("this global::Test.Container.NestedCase error");
		await Assert.That(generated).Contains("Result<TValue, global::Test.NestedCaseError>");
	}

	[Test]
	public async Task GenerateAsync_GivenNoOptIn_EmitsOnlyTheOptInAttribute(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct NotFound(int Id);

				public readonly union NotOptedIn(NotFound);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		result.AssertNoCompilationErrors();

		// The only generated sources are the post-initialization attribute and the embedded attribute.
		await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
		await Assert
			.That(
				result.AllSyntaxTrees.Any(static tree =>
					tree.FilePath.EndsWith("GenerateResultAttribute.g.cs", StringComparison.Ordinal)
				)
			)
			.IsTrue();
	}

	[Test]
	public async Task GenerateAsync_GivenSameUnionsInDifferentSourceOrder_EmitsIdenticalSource(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string first = """
			namespace Test
			{
				public readonly record struct TenantNotFound(int TenantId);

				[GenerateResult]
				public readonly union TenantError(TenantNotFound);
			}
			""";
		const string second = """
			namespace Test
			{
				public readonly record struct RepositoryNotFound(int RepositoryId);

				[GenerateResult]
				public readonly union RepositoryError(RepositoryNotFound);
			}
			""";

		// Act
		var forward = await GenerateAsync([first, second], cancellationToken);
		var reversed = await GenerateAsync([second, first], cancellationToken);

		// Assert
		var forwardSource = string.Join(
			"\n---\n",
			forward
				.PrimarySyntaxTrees.OrderBy(static tree => tree.FilePath, StringComparer.Ordinal)
				.Select(static tree => tree.GetText().ToString())
		);
		var reversedSource = string.Join(
			"\n---\n",
			reversed
				.PrimarySyntaxTrees.OrderBy(static tree => tree.FilePath, StringComparer.Ordinal)
				.Select(static tree => tree.GetText().ToString())
		);

		await Assert.That(forwardSource).IsEqualTo(reversedSource);
	}

	[Test]
	public async Task GenerateAsync_GivenGeneratorDisabled_EmitsNoHelpers(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Test
			{
				public readonly record struct TenantNotFound(int TenantId);

				[GenerateResult]
				public readonly union TenantError(TenantNotFound);
			}
			""";

		// Act
		var result = await GenerateAsync(
			source,
			new ResultsSourceGeneratorTestOptions { DisableSourceGeneratorValue = true },
			cancellationToken
		);

		// Assert
		result.AssertNoCompilationErrors();
		await Assert.That(result.PrimarySyntaxTrees).IsEmpty();
		await Assert
			.That(
				result.LogEntries.Any(static entry =>
					entry.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase)
				)
			)
			.IsTrue();
	}
}
