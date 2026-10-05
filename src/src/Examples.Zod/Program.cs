// Demonstrates Purview.Results.ZodSharp: a ZodSharp validation outcome flows through the same result pipeline
// as every other expected outcome, as a value instead of an exception.
//
//   dotnet run --project src/src/Examples.Zod

TenantRegistrationService service = new();

Heading("1. Valid input succeeds, carrying the value the method produces");

ShowResult("Register('acme-2', 'Acme Two')", service.Register("acme-2", "Acme Two"));
ShowResult("Find(acme)", service.Find(new TenantId("acme")));

Heading("2. Input rejected by its schema becomes a union case carrying every validation error");

var rejected = service.Register(null, "Nameless");

ShowResult("Register(null, 'Nameless')", rejected);
PrintErrors(rejected);

Heading("3. The refinement hook reports a cross-member invariant as a stable code");

var conflicting = service.Register("same", "same");

ShowResult("Register('same', 'same')", conflicting);
PrintErrors(conflicting);

Heading("4. A tenant that already exists fails through exactly the same pipeline");

ShowResult("Register('acme', 'Acme Copy')", service.Register("acme", "Acme Copy"));

Heading("5. A failure short-circuits composition, so nothing downstream runs");

var composed = service
	.Register(null, "Nameless")
	.Map(tenant =>
	{
		Console.WriteLine("      (Map ran: it must not have)");
		return tenant.Name;
	})
	.Tap(name => Console.WriteLine("      (Tap ran: it must not have)"));

var composedText = composed.Match(name => $"Success({name})", DescribeError);

Show("Register(...).Map(name)", composedText);

Heading("6. Probing the failure is how a caller inspects which case it holds");

if (rejected.TryGetError(out var error))
{
	Show("TryGetError", DescribeError(error));

	// A union error is switched on by case type; its own ToString() is only the union's type name.
	if (error is TenantInputInvalid invalid)
	{
		Show("case is TenantInputInvalid", $"{invalid.Errors.Length} validation error(s)");
		Show("the rejected input", invalid.Input);
	}
}

Heading("7. A unit result validates an input without producing a value");

// ToUnitResult drops the validated value: the method accepts an input and reports only why it was rejected.
ShowUnit(
	"Validate('acme-3', 'Acme Three')",
	TenantRegistrationService.Validate(TenantInput.Create("acme-3", "Acme Three"))
);
ShowUnit("Validate('acme-3', null)", TenantRegistrationService.Validate(TenantInput.Create("acme-3", null)));

static void Heading(string title)
{
	Console.WriteLine();
	Console.WriteLine(title);
	Console.WriteLine(new string('-', title.Length));
}

static void Show(string label, object? value) => Console.WriteLine($"  {label, -34} -> {value}");

static void ShowResult<TValue>(string label, Result<TValue, TenantError> result)
{
	var text = result.Match(value => $"Success({value})", DescribeError);

	Console.WriteLine($"  {label, -34} -> {text}");
}

static void ShowUnit(string label, Result<TenantError> result)
{
	// A unit result's success arm takes no value; only the failure arm receives the error.
	var text = result.Match(() => "Success", DescribeError);

	Console.WriteLine($"  {label, -34} -> {text}");
}

static string DescribeError(TenantError error) =>
	error switch
	{
		TenantInputInvalid invalid => $"TenantInputInvalid({invalid.Errors.Length} error(s))",
		TenantNotFound notFound => $"TenantNotFound({notFound.TenantId.Value})",
		TenantDisabled disabled => $"TenantDisabled({disabled.TenantId.Value})",
		TenantAlreadyExists exists => $"TenantAlreadyExists({exists.TenantId.Value})",
		_ => nameof(TenantError),
	};

static void PrintErrors<TValue>(Result<TValue, TenantError> result)
{
	if (!result.TryGetError(out var error) || error is not TenantInputInvalid invalid)
		return;

	foreach (var validationError in invalid.Errors)
	{
		var path = string.Join(".", validationError.Path);

		Console.WriteLine($"      {path}: {validationError.Message} [{validationError.Code}]");
	}
}
