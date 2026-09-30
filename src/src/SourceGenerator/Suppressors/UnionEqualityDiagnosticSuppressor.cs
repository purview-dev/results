using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Purview.Results.SourceGeneration.Diagnostics;

// The language's union support is a preview feature of C# 15; ITypeSymbol.IsUnion is marked
// [Experimental] until the feature ships, and this suppression deliberately targets it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGeneration.Suppressors;

/// <summary>
/// Suppresses <c>CA1815</c> (override <c>Equals</c> and the equality operators on value types) for the union
/// declarations this component generates for.
/// </summary>
/// <remarks>
/// <para>
/// A <c>[GenerateResult]</c> union is the error type of <c>Result&lt;TValue, TError&gt;</c> and is read by
/// matching its case type, never by comparing two unions by value, so the members <c>CA1815</c> asks for are
/// not part of its contract. C# does not synthesise value equality for a union declaration either, so every
/// consumer would otherwise have to repeat a <c>#pragma warning disable CA1815</c> (or a
/// <c>[SuppressMessage]</c>) beside the declaration to keep the build clean.
/// </para>
/// <para>
/// The suppression is deliberately narrow: it applies only where the reported declaration is a union
/// (<see cref="ITypeSymbol.IsUnion"/>, the language's own answer, exactly as the shared diagnostics library
/// uses) <em>and</em> is opted in with <c>[GenerateResult]</c>. A union without the attribute, and every
/// non-union value type, keeps the warning. A consumer who wants union equality declared adds the suppression
/// id to the build's warning suppressions (<c>NoWarn</c>), which brings <c>CA1815</c> back for every union.
/// </para>
/// <para>
/// A suppressor can only suppress non-error, configurable diagnostics, and it never reports a diagnostic of
/// its own, so it cannot hide anything from the diagnostics table in the package README.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionEqualityDiagnosticSuppressor : DiagnosticSuppressor
{
	/// <summary>
	/// The suppression of <c>CA1815</c> on an opted-in union declaration.
	/// </summary>
	/// <remarks>
	/// The id is this component's own namespace of suppression ids (the rules it reports are
	/// <c>RSG1000</c>–<c>RSG1007</c>). It is not a rule: it is the id a consumer adds to <c>NoWarn</c> to turn
	/// the suppression off, and the id every suppression is logged under for auditing.
	/// </remarks>
	public static readonly SuppressionDescriptor Ca1815 = new(
		id: "RSG2000",
		suppressedDiagnosticId: "CA1815",
		justification: "A union declaration opted in with [GenerateResult] is matched by its case type rather than compared by value, and C# does not synthesise value equality for a union declaration, so CA1815 asks for members the union's contract does not need."
	);

	/// <inheritdoc />
	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [Ca1815];

	/// <inheritdoc />
	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach (var diagnostic in context.ReportedDiagnostics)
		{
			// Only CA1815 is declared as suppressible, so this is the same bucket the descriptor names.
			if (!string.Equals(diagnostic.Id, Ca1815.SuppressedDiagnosticId, StringComparison.Ordinal))
				continue;

			if (!IsOptedInUnionDeclaration(context, diagnostic))
				continue;

			context.ReportSuppression(Suppression.Create(Ca1815, diagnostic));
		}
	}

	/// <summary>
	/// Determines whether the reported diagnostic declares an opted-in union.
	/// </summary>
	/// <param name="context">The suppression context, which owns the compilation and semantic models.</param>
	/// <param name="diagnostic">The reported diagnostic, whose location points at the declaration.</param>
	/// <returns><see langword="true"/> when the declaration is a union opted in with <c>[GenerateResult]</c>.</returns>
	/// <remarks>
	/// CA1815 is reported at the declared type's identifier, so the diagnostic's location is resolved to the
	/// nearest enclosing type declaration and then to its symbol. The innermost declaration wins: a union that
	/// nests a non-union type does not suppress the nested type's warning.
	/// </remarks>
	static bool IsOptedInUnionDeclaration(SuppressionAnalysisContext context, Diagnostic diagnostic)
	{
		if (diagnostic.Location.SourceTree is not { } tree)
			return false;

		var root = tree.GetRoot(context.CancellationToken);
		var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

		for (var current = node; current is not null; current = current.Parent)
		{
			if (current is not BaseTypeDeclarationSyntax declaration)
				continue;

			return context.GetSemanticModel(tree).GetDeclaredSymbol(declaration, context.CancellationToken)
					is INamedTypeSymbol { IsUnion: true } union
				&& ResultUnionDiagnostics.HasGenerateResultAttribute(union);
		}

		return false;
	}
}
