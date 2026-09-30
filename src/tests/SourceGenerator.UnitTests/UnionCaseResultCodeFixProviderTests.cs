namespace Purview.Results.SourceGenerator;

public sealed class UnionCaseResultCodeFixProviderTests
{
	const string UnionSource = """
		using Purview.Results;
		using Purview.Results.SourceGeneration;

		namespace Test
		{
			public readonly record struct TenantId(string Value);

			public readonly record struct Tenant(TenantId TenantId);

			public readonly record struct TenantNotFound(TenantId TenantId);

			public readonly record struct Unrelated(int Id);

			[GenerateResult]
			public readonly union TenantError(TenantNotFound);
		}
		""";

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenUnionCaseReturnedFromBlockBody_ShouldOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		var source =
			UnionSource
			+ """

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

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsTrue();
		await Assert.That(result.FixedCode).Contains(".AsFailure<Tenant>()");
		await Assert.That(result.RewriteCompiles).IsTrue();
	}

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenUnionCaseReturnedFromExpressionBody_ShouldOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		var source =
			UnionSource
			+ """

				namespace Test
				{
					public static class Usage
					{
						public static Result<Tenant, TenantError> GetTenant(TenantId tenantId) =>
							new TenantNotFound(tenantId);
					}
				}
				""";

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsTrue();
		await Assert.That(result.FixedCode).Contains(".AsFailure<Tenant>()");
		await Assert.That(result.RewriteCompiles).IsTrue();
	}

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenValueThatIsNotAUnionCase_ShouldNotOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		var source =
			UnionSource
			+ """

				namespace Test
				{
					public static class Usage
					{
						public static Result<Tenant, TenantError> GetTenant()
						{
							return new Unrelated(1);
						}
					}
				}
				""";

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsFalse();
	}

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenUnionWithoutGenerateResult_ShouldNotOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using Purview.Results;

			namespace Test
			{
				public readonly record struct TenantId(string Value);

				public readonly record struct Tenant(TenantId TenantId);

				public readonly record struct TenantNotFound(TenantId TenantId);

				public readonly union TenantError(TenantNotFound);

				public static class Usage
				{
					public static Result<Tenant, TenantError> GetTenant(TenantId tenantId)
					{
						return new TenantNotFound(tenantId);
					}
				}
			}
			""";

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsFalse();
	}

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenNonUnionErrorType_ShouldNotOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		var source =
			UnionSource
			+ """

				namespace Test
				{
					public static class Usage
					{
						public static Result<Tenant, string> GetTenant(TenantId tenantId)
						{
							return new TenantNotFound(tenantId);
						}
					}
				}
				""";

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsFalse();
	}

	[Test]
	public async Task UnionCaseResultCodeFixProvider_GivenUnionNotVisibleBySimpleName_ShouldNotOfferTheFix(
		CancellationToken cancellationToken
	)
	{
		// The helper is generated into the union's own namespace, so a call site that references the union
		// fully qualified without importing that namespace could not bind AsFailure.
		const string source = """
			using Purview.Results;

			namespace Domain
			{
				using Purview.Results.SourceGeneration;

				public readonly record struct TenantId(string Value);

				public readonly record struct Tenant(Domain.TenantId TenantId);

				public readonly record struct TenantNotFound(Domain.TenantId TenantId);

				[GenerateResult]
				public readonly union TenantError(TenantNotFound);
			}

			namespace App
			{
				public static class Usage
				{
					public static Result<Domain.Tenant, Domain.TenantError> GetTenant(Domain.TenantId tenantId)
					{
						return new Domain.TenantNotFound(tenantId);
					}
				}
			}
			""";

		var result = await UnionCodeFixTestHarness.ApplyAsync(source, cancellationToken);

		await Assert.That(result.FixOffered).IsFalse();
	}
}
