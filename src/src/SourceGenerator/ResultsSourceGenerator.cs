using Microsoft.CodeAnalysis;
using Purview.Results.SourceGenerator.Diagnostics;
using Purview.Results.SourceGenerator.Discovery;
using Purview.Results.SourceGenerator.Emit;
using Purview.Results.SourceGenerator.Helpers;
using Purview.Results.SourceGenerator.Models;
using Purview.Results.SourceGenerator.Validation;

namespace Purview.Results.SourceGenerator;

/// <summary>
/// Generates strongly typed helpers that create
/// <c>Result&lt;TValue, TUnion&gt;</c> failures from the case types of C# 15 unions opted in with
/// <c>[GenerateResult]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator is explicit opt-in and does no work for compilations that never apply the attribute:
/// discovery runs through <c>ForAttributeWithMetadataName</c>, and only the post-initialization attribute
/// source is produced for every compilation.
/// </para>
/// <para>
/// The pipeline converts Roslyn symbols into immutable, value-equatable models during discovery, so no
/// syntax nodes, symbols, semantic models or compilations are held in cached pipeline state.
/// </para>
/// </remarks>
[Generator]
public sealed class ResultsSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the incremental pipeline.
	/// </summary>
	/// <param name="context">The generator initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterEmbeddedAttribute<ResultsSourceGenerator>();
		context.RegisterPostInitializationOutput(GenerateResultAttributeEmitter.EmitAttribute);

		var generationContext = IncrementalPipeline.DefaultGenerationContextValueProvider<ResultsSourceGenerator>(
			context,
			PropertyLibrary.DisableGenerator
		);

		var targets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			ResultsTypeLibrary.GenerateResultAttribute,
			static (attributeContext, cancellationToken) =>
				ResultUnionDiscovery.CreateTarget(attributeContext, cancellationToken)
		);

		// Cross-union validation depends on every target, so the models are collected before they are
		// validated and emitted.
		var generationModel = targets.CollectWith(
			generationContext,
			static (results, currentContext, _) => ResultUnionValidator.Validate(results, currentContext),
			"GetResultUnionGenerationModel"
		);

		context.RegisterSourceOutput(
			generationModel,
			static (sourceProductionContext, model) => ExecuteSafely(sourceProductionContext, model)
		);
	}

	/// <summary>
	/// Runs the emitter, reporting an unexpected failure instead of letting it escape.
	/// </summary>
	/// <remarks>
	/// An exception escaping a generator surfaces as <c>CS8785</c> and discards <em>all</em> generated output
	/// for the compilation: one malformed union removes every other union's <c>AsFailure</c> helpers and
	/// factories, producing a cascade of unrelated <c>CS1061</c>/<c>CS0103</c> errors with no indication of
	/// the cause. In the IDE it raises <c>AD0001</c> and analysis stops. Reporting <c>RSG9000</c> instead
	/// keeps the rest of the output intact and names the cause.
	/// <para>
	/// <see cref="OperationCanceledException"/> is deliberately not caught: the IDE cancels generation on
	/// every keystroke, and swallowing that would both report spurious errors and defeat cancellation.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "This is the generator's last line of defence. Any exception that escapes discards "
			+ "every generated source in the compilation (CS8785) and stops IDE analysis (AD0001), so the "
			+ "catch is deliberately broad. Cancellation is rethrown."
	)]
	static void ExecuteSafely(SourceProductionContext context, ResultUnionGenerationModel model)
	{
		try
		{
			ResultUnionEmitter.Execute(context, model);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.UnhandledException,
					location: null,
					"the results source generator",
					exception.Message
				)
			);
		}
	}
}
