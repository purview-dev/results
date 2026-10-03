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

Heading("8. Misuse throws: an uninitialized result is a bug, not a state");

try
{
	Console.WriteLine($"  uninitialized.Value -> {uninitialized.Value}");
}
catch (InvalidOperationException exception)
{
	Console.WriteLine($"  InvalidOperationException: {exception.Message}");
}

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

static string DescribeError(TenantError error) =>
	error switch
	{
		TenantNotFound notFound => $"TenantNotFound({notFound.TenantId.Value})",
		TenantDisabled disabled => $"TenantDisabled({disabled.TenantId.Value})",
		TenantAlreadyExists exists => $"TenantAlreadyExists({exists.TenantId.Value})",
		_ => nameof(TenantError),
	};
