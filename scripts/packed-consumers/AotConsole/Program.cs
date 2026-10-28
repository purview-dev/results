// Packed-consumer gate, consumer (a): a Native AOT console built from the packages alone. Every check records a
// failure rather than throwing, so one run reports every mismatch; any failure makes the process exit non-zero.
using PackedResultsAot;
using Purview.Results;
using Purview.Results.ZodSharp;

var failures = new List<string>();
var passed = 0;

void Check(bool condition, string description)
{
	if (condition)
	{
		passed++;
		Console.WriteLine($"  pass  {description}");
	}
	else
	{
		failures.Add(description);
		Console.WriteLine($"  FAIL  {description}");
	}
}

static Result<string, TenantError> Find(TenantId id) =>
	id.Value == "acme" ? Result<string, TenantError>.Success("Acme") : new TenantNotFound(id).AsFailure<string>();

Console.WriteLine("native image");
Check(System.Runtime.JitInfo.GetCompiledMethodCount() == 0, "the process is a Native AOT image (no JIT)");

Console.WriteLine("result states and combinators");
Result<int, TenantError> uninitialized = default;
Check(!uninitialized.IsInitialized && uninitialized.ToString() == "Uninitialized", "default is Uninitialized");
var found = Find(TenantId.Create("acme")).Map(static name => name.Length);
Check(found.IsSuccess && found.Value == 4, "Map runs on success");
var missing = Find(TenantId.Create("nope")).Map(static name => name.Length);
Check(
	missing.TryGetError(out var error) && error is TenantNotFound { TenantId.Value: "nope" },
	"the generated AsFailure<TValue>() builds a failure carrying the union case"
);
Check(
	TenantError.Failure<int>(new TenantNotFound(TenantId.Create("x"))).IsFailure,
	"the generated union-receiver factory builds a failure"
);
Check(new TenantNotFound(TenantId.Create("x")).AsFailure().IsFailure, "the unit-result AsFailure() helper");

Console.WriteLine("ZodSharp validation folded into results");
var invalid = RenameTenantSchema
	.Validate(new RenameTenant { Name = "x" })
	.ToResult<RenameTenant, TenantError>(static errors => new TenantInputInvalid(errors));
Check(
	invalid.TryGetError(out var invalidError)
		&& invalidError is TenantInputInvalid rejected
		&& rejected.Errors.Select(static e => e.FullPath.ToDottedString()).Order().SequenceEqual(["Name", "Owners"]),
	"ToResult maps a rejected input onto the union case with every error"
);

var valid = await RenameTenantSchema
	.ValidateAsync(new RenameTenant { Name = "Acme", Owners = ["ada"] })
	.ToResultAsync<RenameTenant, TenantError>(static errors => new TenantInputInvalid(errors));
Check(valid.IsSuccess && valid.Value.Name == "Acme", "ValidateAsync folds through ToResultAsync");

var unit = RenameTenantSchema
	.Validate(new RenameTenant { Name = "Acme", Owners = ["ada"] })
	.ToUnitResult<RenameTenant, TenantError>(static errors => new TenantInputInvalid(errors));
Check(unit.IsSuccess && unit.ToString() == "Success", "ToUnitResult discards the value");

Console.WriteLine("a value object's type-level rule through a result");
var tenantId = TenantIdSchema
	.Validate(TenantId.Hydrate(" "))
	.ToResult<TenantId, TenantError>(static errors => new TenantInputInvalid(errors));
Check(
	tenantId.TryGetError(out var tenantIdError)
		&& tenantIdError is TenantInputInvalid { Errors: [{ Code: TenantId.InvalidCode, Origin: "string" }] },
	"the value object's own code and Purview.ZodSharp 3's structured origin reach the result"
);

Console.WriteLine();
if (failures.Count == 0)
{
	Console.WriteLine($"Packed Native AOT consumer passed: {passed} checks.");
	return 0;
}

Console.WriteLine($"Packed Native AOT consumer FAILED: {failures.Count} failure(s), {passed} passed.");
foreach (var failure in failures)
	Console.WriteLine($"  {failure}");

return 1;
