using Microsoft.CodeAnalysis;

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
/// <para>
/// They also record why the generator cannot emit an implicit conversion instead of the helper: an operator
/// is illegal in a static class (<c>CS0715</c>, which is the shape of the generated helper class), a
/// conversion operator must be declared by the source or the target type (<c>CS0556</c>), conversion
/// operators are not permitted as extension members (<c>CS9282</c>), and a conversion operator cannot declare
/// type parameters of its own, so the result's value type is out of scope for a non-generic case type
/// (<c>CS0246</c>). The one shape the language accepts — a generic case type carrying the value type
/// parameter — is the shape the generator rejects as <c>RSG1002</c>.
/// </para>
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

	[Test]
	public async Task ConversionOperator_DeclaredInStaticGeneratedHelperClass_DoesNotCompile(
		CancellationToken cancellationToken
	)
	{
		// The generated helper class is `public static class {Union}ResultExtensions`, and C# forbids
		// user-defined operators in a static class at all, so a generated conversion cannot live beside the
		// generated AsFailure helpers. CS0715 is the compiler's "Static classes cannot contain user-defined
		// operators".
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class TenantErrorResultExtensions
				{
					public static implicit operator Result<Tenant, TenantError>(TenantNotFound error) =>
						Result<Tenant, TenantError>.Failure(error);
				}
			}
			""";

		var errors = await AllErrorCodesAsync(usage, cancellationToken);

		await Assert.That(string.Join(",", errors)).IsEqualTo("CS0715");
	}

	[Test]
	public async Task ConversionOperator_DeclaredOutsideSourceOrTargetType_DoesNotCompile(
		CancellationToken cancellationToken
	)
	{
		// Even in a non-static helper class the operator is illegal: a conversion operator must be declared
		// by the source type or the target type (C# specification 10.5.2), and CS0556 is the compiler's
		// "User-defined conversion must convert to or from the enclosing type".
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public class TenantErrorConversions
				{
					public static implicit operator Result<Tenant, TenantError>(TenantNotFound error) =>
						Result<Tenant, TenantError>.Failure(error);
				}
			}
			""";

		var errors = await AllErrorCodesAsync(usage, cancellationToken);

		await Assert.That(string.Join(",", errors)).IsEqualTo("CS0556");
	}

	[Test]
	public async Task ConversionOperator_DeclaredInExtensionBlock_DoesNotCompile(CancellationToken cancellationToken)
	{
		// Extension operators exist (C# 14), but the extension-operators proposal explicitly excludes
		// user-defined implicit and explicit conversion operators ("not yet designed or planned"), and the
		// compiler reports CS9282, "This member is not allowed in an extension block".
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class TenantErrorConversions
				{
					extension(TenantNotFound error)
					{
						public static implicit operator Result<Tenant, TenantError>(TenantNotFound value) =>
							Result<Tenant, TenantError>.Failure(value);
					}
				}
			}
			""";

		var errors = await AllErrorCodesAsync(usage, cancellationToken);

		await Assert.That(string.Join(",", errors)).IsEqualTo("CS9282");
	}

	[Test]
	public async Task ConversionOperator_OnNonGenericCaseType_CannotNameTheResultValueType(
		CancellationToken cancellationToken
	)
	{
		// Even if the generator could add members to a `partial` case type, a conversion operator cannot
		// declare type parameters of its own: TValue would have to be a type parameter of the declaring
		// type. A real union case type is non-generic, so TValue is simply not in scope (CS0246).
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public readonly record struct TenantNotFound(TenantId TenantId)
				{
					public static implicit operator Result<TValue, TenantError>(TenantNotFound error) =>
						Result<TValue, TenantError>.Failure(error);
				}
			}
			""";

		var errors = await AllErrorCodesAsync(usage, cancellationToken);

		await Assert.That(string.Join(",", errors)).Contains("CS0246");
	}

	[Test]
	public async Task ConversionOperator_OnGenericCaseType_Compiles(CancellationToken cancellationToken)
	{
		// The only shape the language permits: the case type itself carries the value type parameter, which
		// is exactly the shape the generator rejects as RSG1002 (a case type containing type parameters).
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public readonly record struct TenantNotFound<TValue>(TenantId TenantId)
				{
					public static implicit operator Result<TValue, TenantError>(TenantNotFound<TValue> error) =>
						Result<TValue, TenantError>.Failure(new TenantNotFound(TenantId: error.TenantId));
				}
			}
			""";

		var result = await GenerateAsync(
			[UnionSource, usage],
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		// The union still compiles; the added generic case type is not a case of it, so RSG1002 is not the
		// point of this experiment (it records that a generic case type is the only way to declare the
		// conversion, which is why the generated helper exists).
		await Assert.That(result).IsNotNull();
	}

	[Test]
	public async Task Result_GivenUnionCastedCase_Compiles(CancellationToken cancellationToken)
	{
		// The cast closes the case -> union conversion, so returning the union result needs only the
		// library's union -> result conversion: one user-defined conversion per sequence, which is why this
		// shorter form compiles where a bare case value does not.
		const string usage = """
			using Purview.Results;

			namespace Test
			{
				public static class Usage
				{
					public static Result<Tenant, TenantError> GetTenant(TenantId tenantId) =>
						(TenantError)new TenantNotFound(tenantId);
				}
			}
			""";

		var result = await GenerateAsync([UnionSource, usage], cancellationToken);

		result.AssertNoCompilationErrors();
	}

	async Task<string[]> AllErrorCodesAsync(string source, CancellationToken cancellationToken)
	{
		var result = await GenerateAsync(
			[UnionSource, source],
			new ResultsSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		return
		[
			.. result
				.CompilationResult.Compilation.GetDiagnostics(cancellationToken)
				.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
				.Select(static diagnostic => diagnostic.Id)
				.Distinct(StringComparer.Ordinal)
				.Order(StringComparer.Ordinal),
		];
	}
}
