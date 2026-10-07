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
			unions.Add(union);
		}

		var generated = unions.ToImmutable();

		// The per-case AsFailure helper is generated once per case type, so the owner of each case type has
		// to be known before the helpers are decided. The unions are in ordinal order, so the first owner
		// recorded for a case type is the ordinal-first owner.
		Dictionary<TypeReference, List<ResultUnionModel>> caseOwners = [];

		foreach (var union in generated)
		{
			foreach (var unionCase in union.Cases)
			{
				if (!caseOwners.TryGetValue(unionCase.CaseType, out var owners))
				{
					owners = [];
					caseOwners[unionCase.CaseType] = owners;
				}

				owners.Add(union);
			}
		}

		var generatedUnions = ImmutableArray.CreateBuilder<ResultUnionModel>(generated.Length);

		foreach (var union in generated)
		{
			var helperCases = ImmutableArray.CreateBuilder<ResultUnionCaseModel>(union.Cases.Count);

			foreach (var unionCase in union.Cases)
			{
				var owners = caseOwners[unionCase.CaseType];

				if (owners.Count == 1)
				{
					helperCases.Add(unionCase);

					continue;
				}

				if (unionCase.IsUnionCase)
				{
					// A union-typed case is an inclusion: sharing it across unions is the composition pattern,
					// and the union-receiver factory is the safe form. No per-case helper is generated for any
					// owner and no diagnostic is reported, because the helper would bind to the wrong union.
					continue;
				}

				if (owners[0].UnionType.Equals(union.UnionType))
				{
					helperCases.Add(unionCase);

					continue;
				}

				Add(
					diagnostics,
					DiagnosticLibrary.SharedCaseType,
					DiagnosticScope.Compilation,
					union,
					unionCase.FullyQualifiedName,
					owners[0].FullyQualifiedName,
					union.FullyQualifiedName
				);
			}

			// The union is always generated: its union-receiver factory covers every case in union.Cases and
			// every case reachable through an included union, even one shared with another union. Only the
			// per-case AsFailure helpers are dropped, which is why the union keeps its own HelperCases subset.
			generatedUnions.Add(union with { HelperCases = helperCases.ToImmutable() });
		}

		return new ResultUnionGenerationModel(context, generatedUnions.ToImmutable(), diagnostics.ToImmutable());
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
