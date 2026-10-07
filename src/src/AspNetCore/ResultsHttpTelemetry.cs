using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Purview.Results.AspNetCore;

/// <summary>
/// The metric and trace names this package emits under.
/// </summary>
/// <remarks>
/// <para>
/// The design treats an unmapped failure as a host mapping gap rather than a domain outcome, which only
/// works if you can see one happening. Previously the only signal was a log message, so answering "how
/// often is this firing, and for which error" meant scraping log text.
/// </para>
/// <para>
/// This uses <see cref="Meter"/> and <see cref="Activity"/> from the base class library, available through
/// the ASP.NET Core framework reference, so it adds no package dependency — the repository's
/// dependency-light position (no telemetry generator, no <c>Microsoft.Extensions</c> telemetry
/// abstractions) is unchanged.
/// </para>
/// <para>
/// No spans are started. A span per response mapping would be overhead for little gain, and ASP.NET Core
/// already traces the request — so the failure paths add tags to <see cref="Activity.Current"/> instead,
/// which attaches the error type to the trace you already have.
/// </para>
/// <example>
/// Wire the meter into OpenTelemetry:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics.AddMeter(ResultsHttpTelemetry.MeterName));
/// </code>
/// </example>
/// </remarks>
public static class ResultsHttpTelemetry
{
	/// <summary>
	/// The meter name to register with your metrics pipeline.
	/// </summary>
	public const string MeterName = "Purview.Results.AspNetCore";

	/// <summary>
	/// The tag carrying the error's type on a metric measurement and on the current activity.
	/// </summary>
	/// <remarks>
	/// Metrics and traces stay inside your own infrastructure, so unlike the HTTP response — see
	/// <see cref="ResultsHttpOptions.IncludeErrorTypeInProblemDetails"/> — the type name is always included
	/// here. It is the dimension that makes an unmapped-failure count actionable.
	/// </remarks>
	public const string ErrorTypeTag = "purview.results.error_type";

	static readonly Meter Meter = new(MeterName, typeof(ResultsHttpTelemetry).Assembly.GetName().Version?.ToString());

	internal static readonly Counter<long> FailuresMapped = Meter.CreateCounter<long>(
		"purview.results.failures.mapped",
		unit: "{failure}",
		description: "Result failures that a registered mapping or fallback answered."
	);

	internal static readonly Counter<long> FailuresUnmapped = Meter.CreateCounter<long>(
		"purview.results.failures.unmapped",
		unit: "{failure}",
		description: "Result failures with no registered mapping, answered with the unmapped-failure response."
	);

	internal static readonly Counter<long> ResultsUninitialized = Meter.CreateCounter<long>(
		"purview.results.uninitialized",
		unit: "{result}",
		description: "Endpoints that returned a default (uninitialized) result, which is always a bug."
	);

	/// <summary>
	/// Records a failure against <paramref name="counter"/> and tags the current activity with the error type.
	/// </summary>
	internal static void Record(Counter<long> counter, string? errorType)
	{
		KeyValuePair<string, object?> tag = new(ErrorTypeTag, errorType ?? "unknown");

		counter.Add(1, tag);

		// Enriches the request span ASP.NET Core already started rather than creating one.
		Activity.Current?.SetTag(ErrorTypeTag, tag.Value);
	}
}
