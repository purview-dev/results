using Microsoft.CodeAnalysis;
using Purview.Results.SourceGenerator.Discovery;
using Purview.Results.SourceGenerator.Emit;
using Purview.Results.SourceGenerator.Helpers;
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
			static (sourceProductionContext, model) => ResultUnionEmitter.Execute(sourceProductionContext, model)
		);
	}
}
