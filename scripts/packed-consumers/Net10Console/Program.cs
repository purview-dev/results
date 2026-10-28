// Packed-consumer gate, consumer (d): the net10.0 target of the packages, without unions.
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Purview.Results;
using Purview.Results.ZodSharp;
using ZodSharp;
using ZodSharp.Core;

var failures = 0;
void Check(bool condition, string description)
{
	Console.WriteLine($"  {(condition ? "pass" : "FAIL")}  {description}");
	if (!condition)
		failures++;
}

Console.WriteLine($"net10.0 consumer on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Check(Environment.Version.Major == 10, "running on the .NET 10 runtime");

var rejected = InviteSchema
	.Validate(new Invite { Email = "nope" })
	.ToResult<Invite, InviteRejected>(static errors => new InviteRejected(errors));
Check(
	rejected.TryGetError(out var error) && error.Errors.Single().FullPath.ToDottedString() == "Email",
	"ToResult folds a rejected input into the error type"
);

var accepted = InviteSchema
	.Validate(new Invite { Email = "ada@example.com" })
	.ToResult<Invite, InviteRejected>(static errors => new InviteRejected(errors))
	.Map(static invite => invite.Email!.Length);
Check(accepted.IsSuccess && accepted.Value == 15, "and a valid one flows through Map");

return failures == 0 ? 0 : 1;

[ZodSchema]
sealed class Invite
{
	[Required]
	[EmailAddress]
	public string? Email { get; set; }
}

readonly record struct InviteRejected(ImmutableArray<ValidationError> Errors);
