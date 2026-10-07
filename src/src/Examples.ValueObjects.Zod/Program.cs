// Demonstrates composing Purview.ValueObjects with Purview.Results.ZodSharp: a scalar value object's type-level
// ZodSharp rule reports its own code and origin, and the outcome flows through the result pipeline as a value
// instead of an exception.
//
//   dotnet run --project src/src/Examples.ValueObjects.Zod
//
// TenantId is a [Scalar] + [ZodSchema] value object carrying a type-level rule ([NonEmptyTenantId]) whose rule
// implements IZodRule, so it owns the reported code (invalid_tenant_id) and origin (value_object). That identity
// is what lets a host answer the failure by code or by origin without parsing a message.

TenantRegistrationService service = new();
var acmeId = Guid.NewGuid();

Heading("1. A valid identifier registers, carrying the value the method produces");

ShowResult("Register(acmeId, 'Acme')", service.Register(acmeId, "Acme"));

Heading("2. An empty identifier is the value object's sentinel, reported by its type-level rule");

var rejected = service.Register(Guid.Empty, "Nameless");

ShowResult("Register(Guid.Empty, 'Nameless')", rejected);
PrintErrors(rejected);

Heading("3. The rule owns its identity, so a host can answer it by code or by origin");

PrintIdentity(rejected);
Show("code rule", "MapCode(\"invalid_tenant_id\", 422)");
Show("origin rule", "MapOrigin(\"value_object\", 422)");

Heading("4. A duplicate fails through exactly the same pipeline");

ShowResult("Register(acmeId, 'Acme Copy')", service.Register(acmeId, "Acme Copy"));

static void Heading(string title)
{
	Console.WriteLine();
	Console.WriteLine(title);
	Console.WriteLine(new string('-', title.Length));
}

static void Show(string label, object? value) => Console.WriteLine($"  {label, -30} -> {value}");

static void ShowResult<TValue>(string label, Result<TValue, TenantError> result)
{
	var text = result.Match(value => $"Success({value})", DescribeError);

	Console.WriteLine($"  {label, -30} -> {text}");
}

static string DescribeError(TenantError error) =>
	error switch
	{
		TenantInputInvalid invalid => $"TenantInputInvalid({invalid.Errors.Length} error(s))",
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

		Console.WriteLine($"      '{path}': {validationError.Message} [{validationError.Code}]");
	}
}

static void PrintIdentity<TValue>(Result<TValue, TenantError> result)
{
	if (!result.TryGetError(out var error) || error is not TenantInputInvalid invalid)
		return;

	foreach (var validationError in invalid.Errors)
	{
		// A type-level rule validates the value object as a unit, so the path is empty and the origin is the
		// one the rule owns.
		var path = string.Join(".", validationError.Path);

		Console.WriteLine(
			$"      code={validationError.Code}, origin={validationError.Origin ?? "<none>"}, path=[{path}]"
		);
	}
}
