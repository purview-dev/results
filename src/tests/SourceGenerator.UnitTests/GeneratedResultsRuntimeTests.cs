namespace Purview.Results.SourceGenerator;

/// <summary>
/// Executes the helpers generated into this assembly — the generator runs against this project as an
/// analyzer, so the helpers below are the real generated output — and proves the runtime behaviour of the
/// results they produce.
/// </summary>
public class GeneratedResultsRuntimeTests
{
	[Test]
	public async Task AsFailure_GivenNotFoundCase_ProducesInitializedFailure()
	{
		// Arrange
		TenantNotFound tenantNotFound = new(new TenantId("tenant-1"));

		// Act
		// The explicit type is the compile-time proof that the generated helper produces exactly a
		// Result<TValue, TError> for this union; 'var' would hide that contract.
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Tenant, TenantError> result = tenantNotFound.AsFailure<Tenant>();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task AsFailure_GivenNotFoundCase_ExposesTheOriginalCaseAsTheActiveUnionCase()
	{
		// Arrange
		TenantId tenantId = new("tenant-1");
		TenantNotFound tenantNotFound = new(tenantId);

		// Act
		var result = tenantNotFound.AsFailure<Tenant>();

		// Union pattern matching unwraps the union to its active case.
		var activeCase = result.Error switch
		{
			TenantNotFound error => error.TenantId,
			_ => default(TenantId?),
		};

		// Assert
		await Assert.That(activeCase).IsEqualTo(tenantId);
	}

	[Test]
	public async Task AsFailure_GivenDisabledCase_ExposesTheOriginalCaseAsTheActiveUnionCase()
	{
		// Arrange
		TenantId tenantId = new("tenant-2");

		// Act
		var result = new TenantDisabled(tenantId).AsFailure<Tenant>();
		TenantId? activeCase = result.Error is TenantDisabled disabled ? disabled.TenantId : null;

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(activeCase).IsEqualTo(tenantId);
	}

	[Test]
	public async Task AsFailure_GivenAlreadyExistsCase_ExposesTheOriginalCaseAsTheActiveUnionCase()
	{
		// Arrange
		TenantId tenantId = new("tenant-3");

		// Act
		var result = new TenantAlreadyExists(tenantId).AsFailure<Tenant>();
		TenantId? activeCase = result.Error is TenantAlreadyExists alreadyExists ? alreadyExists.TenantId : null;

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(activeCase).IsEqualTo(tenantId);
	}

	[Test]
	public async Task AsFailure_GivenCase_ProducesAFailureWhoseToStringNamesTheState()
	{
		// Arrange
		TenantNotFound tenantNotFound = new(new TenantId("tenant-1"));

		// Act
		var result = tenantNotFound.AsFailure<Tenant>();

		// Assert
		await Assert.That(result.ToString()).StartsWith("Failure(");
	}

	[Test]
	public async Task Result_GivenGeneratedErrorUnion_RemainsUninitializedByDefault()
	{
		// Arrange
		// Act
		Result<Tenant, TenantError> result = default;

		// Assert
		await Assert.That(result.IsInitialized).IsFalse();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.IsFailure).IsFalse();
	}

	[Test]
	public async Task Result_GivenSuccessValueWithGeneratedErrorUnion_IsInitializedSuccess()
	{
		// Arrange
		Tenant tenant = new(new TenantId("tenant-1"));

		// Act
		Result<Tenant, TenantError> result = tenant;

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(tenant);
	}

	[Test]
	public async Task AsFailureUnit_GivenNotFoundCase_ProducesAUnitFailure()
	{
		// Arrange
		TenantNotFound tenantNotFound = new(new TenantId("tenant-1"));

		// Act
		// The explicit type is the compile-time proof that the non-generic helper produces exactly a
		// Result<TError> for this union.
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<TenantError> result = tenantNotFound.AsFailure();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsInitialized).IsTrue();
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task AsFailureUnit_GivenDisabledCase_ProducesAFailureWhoseToStringNamesTheState()
	{
		// Arrange
		// Act
		var result = new TenantDisabled(new TenantId("tenant-2")).AsFailure();

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.ToString()).StartsWith("Failure(");
	}

	[Test]
	public async Task UnionFactoryFailure_GivenNotFoundCase_ProducesAUnitFailure()
	{
		// Arrange
		TenantNotFound tenantNotFound = new(new TenantId("tenant-1"));

		// Act
		// The explicit type is the compile-time proof that the union-receiver factory produces exactly a
		// Result<TError> for the union named by the receiver.
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<TenantError> result = TenantError.Failure(tenantNotFound);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task UnionFactoryValueFailure_GivenNotFoundCase_ProducesAValueFailure()
	{
		// Arrange
		TenantNotFound tenantNotFound = new(new TenantId("tenant-1"));

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Tenant, TenantError> result = TenantError.Failure<Tenant>(tenantNotFound);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task UnionFactoryFailure_GivenIncludedCase_ConstructsTheNestedUnion()
	{
		// Arrange
		// BillingAccountMissing is not a direct case of RegisterTenantError: it is reachable through
		// BillingError, which RegisterTenantError includes. The generated factory constructs the whole
		// nested value.
		BillingAccountMissing missing = new("account-1");

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Tenant, RegisterTenantError> result = RegisterTenantError.Failure<Tenant>(missing);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsFailure).IsTrue();

		// The active leaf is the included case, reached by matching through the included union.
		BillingAccountMissing? activeCase = result.Error switch
		{
			BillingError billing => billing switch
			{
				BillingAccountMissing accountMissing => accountMissing,
				_ => null,
			},
			_ => null,
		};

		await Assert.That(activeCase).IsEqualTo(missing);
	}

	[Test]
	public async Task UnionFactorySuccess_ProducesAUnitSuccess()
	{
		// Arrange
		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<TenantError> result = TenantError.Success();
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task UnionFactoryValueSuccess_GivenValue_ProducesAValueSuccess()
	{
		// Arrange
		Tenant tenant = new(new TenantId("tenant-1"));

		// Act
#pragma warning disable IDE0007 // Use 'var' instead of explicit type
		Result<Tenant, TenantError> result = TenantError.Success(tenant);
#pragma warning restore IDE0007 // Use 'var' instead of explicit type

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(tenant);
	}
}
