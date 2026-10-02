using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Purview.Results.SourceGenerator.Diagnostics;
using Purview.Results.SourceGenerator.Models;

namespace Purview.Results.SourceGenerator.Validation;

/// <summary>
/// Validates the discovered union models against each other and produces the single aggregate generation
/// model.
/// </summary>
/// <remarks>
/// Cross-union rules cannot be evaluated per target, because they depend on whether another union in the
/// same compilation generates the same helper or claims the same case type. Those are the only rules the
/// generator reports; every per-target rule is reported by the analyzer, which consumes the same shared
/// diagnostics library. The blocking policy is shared with it too: findings are created as
/// <see cref="ResultDiagnostic"/> values with the scope they were discovered in.
/// </remarks>
static class ResultUnionValidator
{
	/// <summary>
	/// Validates every discovered union and produces the aggregate generation model.
	/// </summary>
	/// <param name="results">The per-target discovery results, in pipeline order.</param>
	/// <param name="context">The operational generation context.</param>
	/// <returns>
	/// The aggregate model containing the unions to generate for and every diagnostic to report.
	/// </returns>
	public static ResultUnionGenerationModel Validate(
		ImmutableArray<GeneratorResult<ResultUnionModel>> results,
		GeneratorContext context
	)
	{
		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		var candidates = ImmutableArray.CreateBuilder<ResultUnionModel>();

		foreach (var result in results)
		{
			// Findings carried by a discovery result exist to apply the blocking policy to that target
			// (GeneratorResult.ShouldProcess) and are reported by the analyzer, so they are not copied into
			// the generator's output.
			if (result.ShouldProcess)
				candidates.Add(result.Value);
		}

		// Generated output must not depend on the order Roslyn reports targets in, so every union is
		// processed in a deterministic order derived from its own identity.
		var ordered = candidates
			.ToImmutable()
			.Sort(static (left, right) => string.CompareOrdinal(left.FullyQualifiedName, right.FullyQualifiedName));

		var unions = ImmutableArray.CreateBuilder<ResultUnionModel>(ordered.Length);
		HashSet<TypeIdentity> configuredUnions = [];
		Dictionary<string, ResultUnionModel> generatedTypes = [with(StringComparer.Ordinal)];
		Dictionary<TypeReference, ResultUnionModel> caseOwners = [];

		foreach (var union in ordered)
		{
			if (!configuredUnions.Add(union.UnionType))
			{
				// Reported by the analyzer as RSG1004 (non-blocking), and generated once.
				continue;
			}

			var generatedTypeKey = union.Namespace + "|" + union.ExtensionClassName;
			if (generatedTypes.TryGetValue(generatedTypeKey, out var owner))
			{
				Add(
					diagnostics,
					DiagnosticLibrary.GeneratedClassNameCollision,
					DiagnosticScope.Compilation,
					union,
					union.ExtensionClassName,
					union.FullyQualifiedName,
					owner.FullyQualifiedName
				);
				continue;
			}

			generatedTypes[generatedTypeKey] = union;

			var cases = ImmutableArray.CreateBuilder<ResultUnionCaseModel>(union.Cases.Count);

			foreach (var unionCase in union.Cases)
			{
				if (caseOwners.TryGetValue(unionCase.CaseType, out var caseOwner))
				{
					Add(
						diagnostics,
						DiagnosticLibrary.SharedCaseType,
						DiagnosticScope.Compilation,
						union,
						unionCase.FullyQualifiedName,
						caseOwner.FullyQualifiedName
					);
					continue;
				}

				caseOwners[unionCase.CaseType] = union;
				cases.Add(unionCase);
			}

			if (cases.Count == 0)
			{
				// Every case was either shared with another union (reported above) or the union has no cases
				// (reported by the analyzer); either way there is nothing left to generate for this union.
				continue;
			}

			unions.Add(cases.Count == union.Cases.Count ? union : union with { Cases = cases.ToImmutable() });
		}

		return new ResultUnionGenerationModel(context, unions.ToImmutable(), diagnostics.ToImmutable());
	}

	/// <summary>
	/// Creates a compilation-scope finding, letting the shared library decide whether it blocks generation.
	/// </summary>
	static void Add(
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics,
		DiagnosticDescriptor descriptor,
		DiagnosticScope scope,
		ResultUnionModel union,
		params object[] messageArgs
	) =>
		diagnostics.Add(
			ResultDiagnostic
				.Create(descriptor, scope, union.Location.ToLocation(), messageArgs)
				.ToReportableDiagnostic()
		);
}
