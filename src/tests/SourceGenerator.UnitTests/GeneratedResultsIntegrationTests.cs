namespace Purview.Results.SourceGenerator;

/// <summary>
/// Proves that results created by the generated helpers compose with the existing
/// <c>Result&lt;TValue, TError&gt;</c> operations, rather than re-testing those operations.
/// </summary>
public class GeneratedResultsIntegrationTests
{
	static Result<Tenant, TenantError> CreateFailure() =>
		new TenantNotFound(new TenantId("tenant-1")).AsFailure<Tenant>();

	[Test]
	public async Task Match_GivenGeneratedFailure_InvokesTheFailureArmWithTheUnionError()
	{
		// Arrange
		var result = CreateFailure();
		var successInvoked = false;

		// Act
		var matched = result.Match(
			_ =>
			{
				successInvoked = true;
				return "success";
			},
			error => error is TenantNotFound ? "not-found" : "other"
		);

		// Assert
		await Assert.That(matched).IsEqualTo("not-found");
		await Assert.That(successInvoked).IsFalse();
	}

	[Test]
	public async Task Map_GivenGeneratedFailure_DoesNotInvokeTheMappingAndPreservesTheError()
	{
		// Arrange
		var result = CreateFailure();
		var mapInvoked = false;

		// Act
		var mapped = result.Map(tenant =>
		{
			mapInvoked = true;
			return tenant.TenantId.Value;
		});

		// Assert
		await Assert.That(mapInvoked).IsFalse();
		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task MapError_GivenGeneratedFailure_MapsTheUnionError()
	{
		// Arrange
		var result = CreateFailure();
		var mapInvoked = 0;

		// Act
		var mapped = result.MapError(error =>
		{
			mapInvoked++;
			return error is TenantNotFound notFound ? notFound.TenantId.Value : "unknown";
		});

		// Assert
		await Assert.That(mapInvoked).IsEqualTo(1);
		await Assert.That(mapped.IsFailure).IsTrue();
		await Assert.That(mapped.Error).IsEqualTo("tenant-1");
	}

	[Test]
	public async Task Bind_GivenGeneratedFailure_DoesNotInvokeTheBinderAndPreservesTheError()
	{
		// Arrange
		var result = CreateFailure();
		var bindInvoked = false;

		// Act
		var bound = result.Bind(tenant =>
		{
			bindInvoked = true;
			return Result<TenantId, TenantError>.Success(tenant.TenantId);
		});

		// Assert
		await Assert.That(bindInvoked).IsFalse();
		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task Bind_GivenGeneratedFailureComposedWithASecondGeneratedFailure_PreservesTheFirstError()
	{
		// Arrange
		var result = CreateFailure();

		// Act
		var bound = result.Bind(_ => new TenantDisabled(new TenantId("tenant-9")).AsFailure<TenantId>());

		// Assert
		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error is TenantNotFound).IsTrue();
	}

	[Test]
	public async Task Map_GivenGeneratedSuccess_InvokesTheMapping()
	{
		// Arrange
		Result<Tenant, TenantError> result = new Tenant(new TenantId("tenant-1"));

		// Act
		var mapped = result.Map(tenant => tenant.TenantId.Value);

		// Assert
		await Assert.That(mapped.IsSuccess).IsTrue();
		await Assert.That(mapped.Value).IsEqualTo("tenant-1");
	}

	[Test]
	public async Task Bind_GivenBillingFailure_ShouldWidenTheErrorIntoTheCompositeUnion()
	{
		// Arrange
		var billing = new BillingServiceUnavailable("down").AsFailure<Tenant>();

		// Act
		// The widening Bind maps the billing error into RegisterTenantError, so the binder may return the
		// composite error type directly. The lambda returns the error unchanged; the union conversion lifts
		// BillingError into RegisterTenantError.
		var bound = billing.Bind(
			_ => Result<TenantId, RegisterTenantError>.Success(new TenantId("tenant-1")),
			error => error
		);

		// Assert
		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error is BillingError).IsTrue();
	}

	[Test]
	public async Task Bind_GivenBillingSuccess_ShouldReturnTheBoundResult()
	{
		// Arrange
		var billing = Result<Tenant, BillingError>.Success(new Tenant(new TenantId("t")));

		// Act
		var bound = billing.Bind(
			// A tenant error is a case of the composite union, so its generated helper produces the composite
			// failure directly once the case has been lifted to TenantError.
			_ => ((TenantError)new TenantAlreadyExists(new TenantId("t"))).AsFailure<TenantId>(),
			error => error
		);

		// Assert
		await Assert.That(bound.IsFailure).IsTrue();
		await Assert.That(bound.Error is TenantError).IsTrue();
	}

	[Test]
	public async Task AsFailure_GivenNestedBillingUnionCase_ShouldProduceTheCompositeFailure()
	{
		// Arrange
		var billing = new BillingServiceUnavailable("down").AsFailure<Tenant>();

		// Act
		// The guard idiom: a whole union case of the composite union has its own AsFailure helper, so the
		// billing failure lifts into the registration contract without an explicit MapError.
		await Assert.That(billing.TryGetError(out var billingError)).IsTrue();
		var failure = billingError.AsFailure<TenantId>();

		// Assert
		await Assert.That(failure.IsFailure).IsTrue();
		await Assert.That(failure.Error is BillingError).IsTrue();
	}

	[Test]
	public async Task Throw_GivenGeneratedFailure_ShouldThrowResultExceptionCarryingTheUnion()
	{
		// Arrange
		var result = new TenantNotFound(new TenantId("tenant-1")).AsFailure<Tenant>();
		ResultException<TenantError>? exception = null;

		// Act
		try
		{
			result.Throw();
		}
		catch (ResultException<TenantError> caught)
		{
			exception = caught;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Error is TenantNotFound).IsTrue();
	}
}
