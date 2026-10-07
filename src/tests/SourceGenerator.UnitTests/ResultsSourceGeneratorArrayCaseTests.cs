namespace Purview.Results.SourceGenerator;

/// <summary>
/// Covers an array-typed union case.
/// </summary>
/// <remarks>
/// The discovery code explicitly handles <c>IArrayTypeSymbol</c> when deciding accessibility and when
/// looking for type parameters, and then cast the case type to <c>INamedTypeSymbol</c> — which an array is
/// not. An array-typed case therefore threw <see cref="InvalidCastException"/> out of the generator, and
/// because there is no catch-all in the pipeline the compiler reported <c>CS8785</c> and dropped
/// <em>all</em> generated output for the compilation: every <c>AsFailure</c> helper and union factory
/// vanished, producing a cascade of unrelated errors with no indication of the real cause. In the IDE the
/// analyzer raised <c>AD0001</c> and stopped analysing.
/// </remarks>
public class ResultsSourceGeneratorArrayCaseTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	const string ArrayCaseSource = """
		namespace Test
		{
			public readonly record struct TenantId(string Value);

			public readonly record struct TenantNotFound(TenantId TenantId);

			[GenerateResult]
			public readonly union TenantError(TenantNotFound, string[]);
		}
		""";

	[Test]
	public async Task GenerateAsync_GivenArrayTypedCase_DoesNotThrowOutOfTheGenerator(
		CancellationToken cancellationToken
	)
	{
		// Act — the generator must report or skip, never crash. A crash takes every other union in the
		// compilation down with it.
		var result = await GenerateAsync(ArrayCaseSource, cancellationToken);

		// Assert
		await Assert.That(result).IsNotNull();
	}

	[Test]
	public async Task GenerateAsync_GivenArrayTypedCase_StillEmitsHelpersForTheValidCases(
		CancellationToken cancellationToken
	)
	{
		// Act
		var result = await GenerateAsync(ArrayCaseSource, cancellationToken);

		// Assert — the named case is unaffected by the unusable one.
		var generated = result.AssertSingleGeneratedSource();

		await Assert.That(generated).Contains("this global::Test.TenantNotFound error");
	}
}
