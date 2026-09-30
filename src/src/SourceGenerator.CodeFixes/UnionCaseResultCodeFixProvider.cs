using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

// ITypeSymbol.IsUnion is [Experimental] until C# 15 union support ships, and this provider targets it.
#pragma warning disable RSEXPERIMENTAL006

namespace Purview.Results.SourceGeneration.CodeFixes;

/// <summary>
/// Offers the generated <c>AsFailure&lt;TValue&gt;()</c> helper when a union case value is returned where a
/// <c>Result&lt;TValue, TError&gt;</c> is expected (<c>CS0029</c>).
/// </summary>
/// <remarks>
/// <para>
/// The generator cannot make a case value convert implicitly: C# forbids user-defined operators in a static
/// class, forbids conversion operators in extension members, allows only one user-defined conversion per
/// conversion sequence, and a conversion operator cannot declare the result's value type parameter. A code fix
/// is therefore the only way to spare the caller from writing the helper by hand. The compiler evidence is
/// recorded in <c>UnionCompilerBehaviourTests</c> in this repository.
/// </para>
/// <para>
/// A fix is only offered when the rewritten call will actually bind: the converted type must be
/// <c>Purview.Results.Result&lt;TValue, TError&gt;</c>, <c>TError</c> must be a union opted in with
/// <c>[GenerateResult]</c> (so the helper exists), the expression must be one of that union's case values, and
/// the union must be reachable by its simple name at the call site (the helper is generated into the union's
/// own namespace).
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnionCaseResultCodeFixProvider)), Shared]
public sealed class UnionCaseResultCodeFixProvider : CodeFixProvider
{
	const string EquivalenceKey = "PurviewResultsUseAsFailure";
	const string ResultNamespace = "Purview.Results";
	const string ResultMetadataName = "Result`2";
	const string SourceGenerationNamespace = "Purview.Results.SourceGeneration";
	const string GenerateResultAttributeMetadataName = "GenerateResultAttribute";
	const string FailureHelperName = "AsFailure";

	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds => ["CS0029"];

	/// <inheritdoc />
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc />
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var cancellationToken = context.CancellationToken;
		var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		var model = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

		if (root is null || model is null)
			return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var expression = FindExpression(root, diagnostic.Location.SourceSpan);
			if (expression is null)
				continue;

			var (valueType, unionType) = ResolveResultTarget(model, expression, cancellationToken);
			if (valueType is null || unionType is null)
				continue;

			// The expression must be a case of the union, otherwise AsFailure<TValue>() would not exist for
			// it and the rewrite would not compile.
			if (!IsCaseOf(unionType, model.GetTypeInfo(expression, cancellationToken).Type))
				continue;

			// The helper is generated into the union's namespace, so the rewrite only binds where that
			// namespace is reachable by simple name.
			if (!IsUnionVisibleBySimpleName(model, unionType, expression.SpanStart))
				continue;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Convert the union case to a failed result with {FailureHelperName}<{valueType.Name}>()",
					createChangedDocument: token => UseAsFailureAsync(context.Document, expression, valueType, token),
					equivalenceKey: EquivalenceKey
				),
				diagnostic
			);
		}
	}

	static ExpressionSyntax? FindExpression(SyntaxNode root, TextSpan span)
	{
		var node = root.FindNode(span, getInnermostNodeForTie: true);

		return node as ExpressionSyntax ?? node.FirstAncestorOrSelf<ExpressionSyntax>();
	}

	static (ITypeSymbol? ValueType, INamedTypeSymbol? UnionType) ResolveResultTarget(
		SemanticModel model,
		ExpressionSyntax expression,
		CancellationToken cancellationToken
	)
	{
		if (model.GetTypeInfo(expression, cancellationToken).ConvertedType is not INamedTypeSymbol converted)
			return (null, null);

		if (converted.Arity != 2 || !HasMetadataName(converted, ResultNamespace, ResultMetadataName))
			return (null, null);

		if (
			converted.TypeArguments[1] is not INamedTypeSymbol union
			|| !union.IsUnion
			|| !HasGenerateResultAttribute(union)
		)
			return (null, null);

		return (converted.TypeArguments[0], union);
	}

	static bool HasGenerateResultAttribute(INamedTypeSymbol union)
	{
		foreach (var attribute in union.GetAttributes())
		{
			if (
				attribute.AttributeClass is { } attributeClass
				&& HasMetadataName(attributeClass, SourceGenerationNamespace, GenerateResultAttributeMetadataName)
			)
			{
				return true;
			}
		}

		return false;
	}

	static bool IsCaseOf(INamedTypeSymbol union, ITypeSymbol? caseType)
	{
		if (caseType is null)
			return false;

		foreach (var constructor in union.InstanceConstructors)
		{
			if (
				constructor.DeclaredAccessibility == Accessibility.Public
				&& constructor.Parameters.Length == 1
				&& constructor.Parameters[0].RefKind is RefKind.None or RefKind.In
				&& SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, caseType)
			)
			{
				return true;
			}
		}

		return false;
	}

	static bool IsUnionVisibleBySimpleName(SemanticModel model, INamedTypeSymbol union, int position)
	{
		if (union.ContainingNamespace.IsGlobalNamespace)
			return true;

		foreach (var symbol in model.LookupSymbols(position, name: union.Name))
		{
			if (SymbolEqualityComparer.Default.Equals(symbol, union))
				return true;
		}

		return false;
	}

	static bool HasMetadataName(INamedTypeSymbol type, string @namespace, string metadataName) =>
		type.MetadataName == metadataName && type.ContainingNamespace.ToDisplayString() == @namespace;

	static async Task<Document> UseAsFailureAsync(
		Document document,
		ExpressionSyntax expression,
		ITypeSymbol valueType,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var typeArgument = SyntaxFactory
			.ParseTypeName(valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
			.WithAdditionalAnnotations(Simplifier.Annotation);

		var invocation = SyntaxFactory
			.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					expression.WithoutTrivia(),
					SyntaxFactory.GenericName(
						SyntaxFactory.Identifier(FailureHelperName),
						SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArgument))
					)
				)
			)
			.WithTriviaFrom(expression)
			.WithAdditionalAnnotations(Formatter.Annotation);

		return document.WithSyntaxRoot(root.ReplaceNode(expression, invocation));
	}
}
