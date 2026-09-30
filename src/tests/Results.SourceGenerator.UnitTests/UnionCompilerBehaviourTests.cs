using Microsoft.CodeAnalysis;
using Purview.Results.SourceGeneration;

// The language's union support is a preview feature of C# 15; ITypeSymbol.IsUnion is marked
// [Experimental] until the feature ships, and these tests deliberately exercise it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Compiler experiments that record the C# 15 union symbol model and the conversion behaviour that
/// motivates the generator.
/// </summary>
/// <remarks>
/// These tests compile real C# rather than inferring the language's behaviour. They prove that a declared
/// union is recognised through <c>ITypeSymbol.IsUnion</c> and exposes one public single-parameter
/// constructor per case type (which is how the generator resolves its case set), that C# composes a case
/// type into the union and the union into a result but not both conversions at once (which is why an
/// explicit generated helper is required), and that the explicit forms the helper replaces do compile.
/// </remarks>
public class UnionCompilerBehaviourTests
	: TUnitSourceGeneratorTestBase<ResultsSourceGenerator, ResultsSourceGeneratorTestOptions>
{
	const string UnionSource = """
		namespace Test
		{
			public readonly record struct TenantId(string Value);

			public readonly record struct Tenant(TenantId TenantId);

			public readonly record struct TenantNotFound(TenantId TenantId);

			public readonly record struct TenantDisabled(TenantId TenantId);

			public readonly record struct TenantAlreadyExists(TenantId TenantId);

			public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
		}
		""";

	[Test]
	public async Task Union_GivenDeclaredUnion_ExposesExpectedSemanticShape(CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(UnionSource, cancellationToken);

		result.AssertNoCompilationErrors();

		var unionSymbol = (INamedTypeSymbol)result.GetTypeByMetadataName("Test.TenantError")!;

		var interfaces = string.Join(
			",",
			unionSymbol.AllInterfaces.Select(static type => type.ToDisplayString()).Order(StringComparer.Ordinal)
		);
		var cases = string.Join(
			",",
			unionSymbol
				.InstanceConstructors.Where(static constructor =>
					constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 1
				)
				.Select(static constructor => constructor.Parameters[0].Type.ToDisplayString())
				.Order(StringComparer.Ordinal)
		);

		await Assert.That(unionSymbol.IsUnion).IsTrue();
		await Assert.That(unionSymbol.TypeKind).IsEqualTo(TypeKind.Struct);
		await Assert.That(interfaces).Contains("System.Runtime.CompilerServices.IUnion");
		await Assert.That(cases).IsEqualTo("Test.TenantAlreadyExists,Test.TenantDisabled,Test.TenantNotFound");
	}

	[Test]
	public async Task Result_GivenCaseValue_DirectAssignmentDoesNotCompile(CancellationToken cancellationToken)
	{
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class Usage
				{
					public static Result<Tenant, TenantError> GetTenant(TenantId tenantId)
					{
						return new TenantNotFound(tenantId);
					}
				}
			}
			""";

		var result = await GenerateAsync(
			[UnionSource, usage],
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		var errors = result
			.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
			.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
			.Select(static diagnostic => diagnostic.Id)
			.ToArray();

		// C# never composes the union conversion (TenantNotFound -> TenantError) with the result's
		// user-defined conversion (TenantError -> Result<...>), so the assignment reports CS0029. This is
		// the motivating case for the generated AsFailure helper.
		await Assert.That(errors).Contains("CS0029");
	}

	[Test]
	public async Task Result_GivenExplicitFailureForms_Compiles(CancellationToken cancellationToken)
	{
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class Usage
				{
					public static Result<Tenant, TenantError> FromGenericFailure(TenantId tenantId) =>
						Result<Tenant, TenantError>.Failure(new TenantNotFound(tenantId));

					public static Result<Tenant, TenantError> FromResultFactory(TenantId tenantId) =>
						Result.Failure<Tenant, TenantError>(new TenantNotFound(tenantId));

					public static Result<Tenant, TenantError> FromLocal(TenantId tenantId)
					{
						TenantError error = new TenantNotFound(tenantId);
						return error;
					}
				}
			}
			""";

		var result = await GenerateAsync([UnionSource, usage], cancellationToken);

		result.AssertNoCompilationErrors();
	}

	[Test]
	public async Task Result_GivenExtensionMethodOnUnionReceiver_DoesNotComposeWithCaseValues(
		CancellationToken cancellationToken
	)
	{
		// Explores the alternative generated API shape of a single extension method on the union type.
		// C# does not apply union conversions to an extension-method receiver, so this shape cannot support
		// `new TenantNotFound(id).AsFailure<Tenant>()`; the generator therefore emits one helper per case
		// type, whose receiver is the case type itself.
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class UnionExtensions
				{
					public static Result<TValue, TenantError> AsFailureFromUnion<TValue>(this TenantError error) =>
						Result<TValue, TenantError>.Failure(error);
				}

				public static class Usage
				{
					public static Result<Tenant, TenantError> FromCase(TenantId tenantId) =>
						new TenantNotFound(tenantId).AsFailureFromUnion<Tenant>();
				}
			}
			""";

		var result = await GenerateAsync(
			[UnionSource, usage],
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		var errors = result
			.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
			.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
			.Select(static diagnostic => diagnostic.Id)
			.ToArray();

		await Assert.That(errors).Contains("CS1929");
	}
}
