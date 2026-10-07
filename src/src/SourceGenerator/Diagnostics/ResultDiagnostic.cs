using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.Results.SourceGenerator.Diagnostics;

/// <summary>
/// A single diagnostic finding produced by the shared diagnostics library.
/// </summary>
/// <remarks>
/// <para>
/// Findings are transient: they carry a Roslyn <see cref="Location"/>, so they are produced during
/// discovery/analysis and converted immediately by their host. They are never stored in the incremental
/// pipeline's cached models — the generator converts them to a <c>ReportableDiagnostic</c>, which keeps
/// only the file path and spans.
/// </para>
/// <para>
/// The blocking decision is not made at the call site: <see cref="IsBlocking"/> asks
/// <see cref="DiagnosticLibrary.IsBlocking"/> with the scope the finding was discovered in, so the analyzer
/// and the generator can never disagree about whether generation stops.
/// </para>
/// </remarks>
/// <param name="Descriptor">The rule that was violated.</param>
/// <param name="Location">The location the finding is reported at.</param>
/// <param name="Scope">The scope the finding was discovered in.</param>
/// <param name="MessageArgs">The message arguments for the descriptor's message format.</param>
readonly record struct ResultDiagnostic(
	DiagnosticDescriptor Descriptor,
	Location Location,
	DiagnosticScope Scope,
	ImmutableArray<object> MessageArgs
)
{
	/// <summary>
	/// Gets the rule id of the finding.
	/// </summary>
	public string Id => Descriptor.Id;

	/// <summary>
	/// Gets the severity of the finding.
	/// </summary>
	public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;

	/// <summary>
	/// Gets whether this finding stops generation for the affected target.
	/// </summary>
	public bool IsBlocking => DiagnosticLibrary.IsBlocking(Descriptor, Scope);

	/// <summary>
	/// Creates a finding for one descriptor, scope and optional location.
	/// </summary>
	/// <param name="descriptor">The rule that was violated.</param>
	/// <param name="scope">The scope the finding was discovered in.</param>
	/// <param name="location">The location the finding is reported at, or <see langword="null"/>.</param>
	/// <param name="messageArgs">The message arguments for the descriptor's message format.</param>
	/// <returns>The finding.</returns>
	public static ResultDiagnostic Create(
		DiagnosticDescriptor descriptor,
		DiagnosticScope scope,
		Location? location,
		params object[] messageArgs
	) => new(descriptor, location ?? Location.None, scope, messageArgs is null ? [] : [.. messageArgs]);

	/// <summary>
	/// Creates the Roslyn diagnostic reported by the analyzer.
	/// </summary>
	/// <returns>The analyzer-facing diagnostic.</returns>
	public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location, MessageArgs.ToArray());

	/// <summary>
	/// Creates the pipeline-safe diagnostic carried by the source generator, which keeps the location's file
	/// path and spans rather than the <see cref="Location"/> itself.
	/// </summary>
	/// <returns>The generator-facing diagnostic.</returns>
	public ReportableDiagnostic ToReportableDiagnostic() =>
		ReportableDiagnostic.Create(Descriptor, IsBlocking, Location, MessageArgs.ToArray());
}
