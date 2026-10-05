// A guided tour of Purview.Results over the Tenancy domain the package README documents.
//
//   dotnet run --project src/src/Examples.Basic
//
// Every expected outcome below is a value. Nothing throws except the deliberate misuse at the end, which
// demonstrates the throw-on-misuse contract that keeps an uninitialized result a bug rather than a state.

InMemoryTenantStore store = new();
TenantId acmeId = new("acme");
TenantId globexId = new("globex");
TenantId missingId = new("initech");

Heading("1. Success and failure are values");

ShowResult("GetTenant(acme)", store.GetTenant(acmeId));
ShowResult("GetTenant(initech)", store.GetTenant(missingId));

Heading("2. Match folds both states into one value");

foreach (var id in new[] { acmeId, globexId, missingId })
{
	var description = store.GetTenant(id).Match(tenant => $"loaded '{tenant.Name}'", DescribeError);

	Console.WriteLine($"  {id.Value, -8} -> {description}");
}

Heading("3. Map, Bind and MapError transform without unwrapping");

ShowResult("Map(name)", store.GetTenant(acmeId).Map(tenant => tenant.Name));
ShowResult(
	"Bind(create 'newco')",
	store.GetTenant(acmeId).Bind(tenant => store.CreateTenant(new TenantId("newco"), tenant.Name))
);

// MapError widens the error type, so the mapped result is printed as-is rather than through ShowResult.
Show(
	"MapError(to message)",
	store.GetTenant(missingId).MapError(error => $"the tenant operation failed: {DescribeError(error)}")
);

Heading("4. Ensure turns a success that breaks a rule into a failure");

ShowResult(
	"Ensure(tenant is enabled)",
	store.GetTenant(globexId).Ensure(tenant => tenant.Enabled, tenant => new TenantDisabled(tenant.Id))
);

Heading("5. Probing never throws, so even an uninitialized result can be inspected");

var uninitialized = default(Result<Tenant, TenantError>);

Show("default", uninitialized);
Show("IsInitialized", uninitialized.IsInitialized);
Show("TryGetValue", uninitialized.TryGetValue(out _));
Show("TryGetError", uninitialized.TryGetError(out _));

Heading("6. Success and failure also come from factories and implicit conversions");

// The declared type is what lets the implicit conversion apply, so these two cannot use `var`.
Result<Tenant, TenantError> fromValue = store.GetTenant(acmeId).Value;
Result<Tenant, TenantError> fromError = (TenantError)new TenantAlreadyExists(acmeId);
var fromFactory = Result<Tenant, TenantError>.Failure(new TenantDisabled(acmeId));

ShowResult("implicit value", fromValue);
ShowResult("implicit error", fromError);
ShowResult("error factory", fromFactory);

Heading("7. The generated AsFailure<TValue>() helper is what makes union cases ergonomic");

// `return new TenantNotFound(id);` does not compile (CS0029): the case converts to the union and the union
// converts to the result, but C# never composes two user-defined conversions. The generator emits one helper
// per case of every [GenerateResult] union, which is the ergonomics the language allows.
var helper = new TenantNotFound(missingId).AsFailure<Tenant>();

ShowResult("AsFailure<Tenant>()", helper);

Heading("8. Chain services by widening one error union into another");

TenantRegistrationService registration = new(store, new BillingService());
TenantId hooliId = new("hooli");

// RegisterTenantError has TenantError and BillingError as cases, so a failure from either service flows
// through one result type; the widening Bind lifts a billing failure into the registration union.
ShowRegistration("Register(hooli)", registration.Register(hooliId, "Hooli"));
ShowRegistration("Register(globex)", registration.Register(globexId, "Globex"));
ShowRegistration("Register(hooli) again", registration.Register(hooliId, "Hooli"));
ShowRegistration("guard(hooli)", registration.RegisterWithGuard(hooliId, "Hooli"));

Heading("9. Throw turns an expected failure into an exception at a boundary that must throw");

try
{
	store.GetTenant(missingId).Throw();
}
catch (ResultException<TenantError> exception)
{
	// The exception exposes the error with its original type, so it can be described like any other failure.
	Console.WriteLine($"  ResultException<TenantError>: {DescribeError(exception.Error)}");
}

Heading("10. Misuse throws: an uninitialized result is a bug, not a state");

try
{
	Console.WriteLine($"  uninitialized.Value -> {uninitialized.Value}");
}
catch (InvalidOperationException exception)
{
	Console.WriteLine($"  InvalidOperationException: {exception.Message}");
}

Heading("11. A unit Result<TError> succeeds with no value, so only the failure has to be carried");

// A command that either completes or explains why not is a unit result: its success is the Success marker
// rather than a value. DeleteTenant returns Result<TenantError>, and the generated AsFailure() helper (with
// no value type parameter) builds one from a union case.
ShowUnit("DeleteTenant(hooli)", store.DeleteTenant(hooliId));
ShowUnit("DeleteTenant(hooli) again", store.DeleteTenant(hooliId));
ShowUnit("Result<TError>.Success()", Result<TenantError>.Success());

static void Heading(string title)
{
	Console.WriteLine();
	Console.WriteLine(title);
	Console.WriteLine(new string('-', title.Length));
}

static void Show(string label, object? value) => Console.WriteLine($"  {label, -28} -> {value}");

static void ShowResult<TValue>(string label, Result<TValue, TenantError> result)
{
	// Matching on the result gives a readable failure: a union's own ToString() is the union's type name,
	// not the active case, so DescribeError switches on the case types instead.
	var text = result.Match(value => $"Success({value})", DescribeError);

	Console.WriteLine($"  {label, -28} -> {text}");
}

static void ShowUnit(string label, Result<TenantError> result)
{
	// A unit result's success arm takes no value; only the failure arm receives the error.
	var text = result.Match(() => "Success", DescribeError);

	Console.WriteLine($"  {label, -28} -> {text}");
}

static string DescribeError(TenantError error) =>
	error switch
	{
		TenantNotFound notFound => $"TenantNotFound({notFound.TenantId.Value})",
		TenantDisabled disabled => $"TenantDisabled({disabled.TenantId.Value})",
		TenantAlreadyExists exists => $"TenantAlreadyExists({exists.TenantId.Value})",
		_ => nameof(TenantError),
	};

static void ShowRegistration(string label, Result<Tenant, RegisterTenantError> result)
{
	var text = result.Match(value => $"Success({value.Name})", DescribeRegistrationError);

	Console.WriteLine($"  {label, -28} -> {text}");
}

static string DescribeRegistrationError(RegisterTenantError error) =>
	error switch
	{
		TenantError tenantError => DescribeError(tenantError),
		BillingError billingError => DescribeBillingError(billingError),
		_ => nameof(RegisterTenantError),
	};

static string DescribeBillingError(BillingError error) =>
	error switch
	{
		BillingAccountMissing missing => $"BillingAccountMissing({missing.TenantId.Value})",
		BillingServiceUnavailable unavailable => $"BillingServiceUnavailable({unavailable.Reason})",
		_ => nameof(BillingError),
	};
