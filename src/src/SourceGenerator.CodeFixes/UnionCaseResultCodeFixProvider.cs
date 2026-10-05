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

namespace Purview.Results.SourceGenerator.CodeFixes;

/// <summary>
/// Offers the generated union-receiver factories <c>Union.Failure&lt;TValue&gt;(case)</c> and
/// <c>Union.Failure(case)</c> when a union case value is returned where a
/// <c>Result&lt;TValue, TError&gt;</c> or a unit <c>Result&lt;TError&gt;</c> is expected (<c>CS0029</c>).
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
/// The fix names the union by its receiver (<c>Union.Failure(case)</c>) rather than the case, because the
/// union-receiver factory is generated for every case of every union. The per-case <c>AsFailure</c> helpers
/// are generated once when a case type is shared with another union, so a case-receiver rewrite could bind to
/// the wrong union; the factory cannot.
/// </para>
/// <para>
/// A fix is only offered when the rewritten call will actually bind: the converted type must be
/// <c>Purview.Results.Result&lt;TValue, TError&gt;</c> or <c>Purview.Results.Result&lt;TError&gt;</c>,
/// <c>TError</c> must be a union opted in with <c>[GenerateResult]</c> (so the factory exists), the expression
/// must be one of that union's case values — including a case reached through an included union, because the
/// factory covers those too — and the union must be reachable by its simple name at the call site (the factory
/// is generated into the union's own namespace). A value result gets
/// <c>Union.Failure&lt;TValue&gt;(case)</c>; a unit result gets <c>Union.Failure(case)</c>.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnionCaseResultCodeFixProvider)), Shared]
public sealed class UnionCaseResultCodeFixProvider : CodeFixProvider
{
	const string EquivalenceKey = "PurviewResultsUseUnionFailureFactory";
	const string ResultNamespace = "Purview.Results";
	const string ResultMetadataName = "Result`2";
	const string ResultUnitMetadataName = "Result`1";
	const string GenerateResultAttributeMetadataName = "GenerateResultAttribute";
	const string FailureFactoryName = "Failure";

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

			var (valueType, unionType, isUnit) = ResolveResultTarget(model, expression, cancellationToken);
			if (unionType is null || (!isUnit && valueType is null))
				continue;

			// The expression must be a case of the union, otherwise AsFailure<TValue>() would not exist for
			// it and the rewrite would not compile.
			if (!IsCaseOf(unionType, model.GetTypeInfo(expression, cancellationToken).Type))
				continue;

			// The helper is generated into the union's namespace, so the rewrite only binds where that
			// namespace is reachable by simple name.
			if (!IsUnionVisibleBySimpleName(model, unionType, expression.SpanStart))
				continue;

			var title = isUnit
				? $"Convert the union case to a failed unit result with {unionType.Name}.{FailureFactoryName}()"
				: $"Convert the union case to a failed result with {unionType.Name}.{FailureFactoryName}<{valueType!.Name}>()";

			context.RegisterCodeFix(
				CodeAction.Create(
					title: title,
					createChangedDocument: token =>
						UseUnionFactoryAsync(context.Document, expression, unionType, isUnit ? null : valueType, token),
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

	static (ITypeSymbol? ValueType, INamedTypeSymbol? UnionType, bool IsUnit) ResolveResultTarget(
		SemanticModel model,
		ExpressionSyntax expression,
		CancellationToken cancellationToken
	)
	{
		if (model.GetTypeInfo(expression, cancellationToken).ConvertedType is not INamedTypeSymbol converted)
			return (null, null, false);

		var isValueResult = converted.Arity == 2 && HasMetadataName(converted, ResultNamespace, ResultMetadataName);
		var isUnitResult = converted.Arity == 1 && HasMetadataName(converted, ResultNamespace, ResultUnitMetadataName);

		if (!isValueResult && !isUnitResult)
			return (null, null, false);

		// The union is the last type argument in both shapes: Result<TValue, TUnion> and Result<TUnion>.
		if (
			converted.TypeArguments[converted.Arity - 1] is not INamedTypeSymbol union
			|| !union.IsUnion
			|| !HasGenerateResultAttribute(union)
		)
			return (null, null, false);

		return isUnitResult ? (null, union, true) : (converted.TypeArguments[0], union, false);
	}

	static bool HasGenerateResultAttribute(INamedTypeSymbol union)
	{
		foreach (var attribute in union.GetAttributes())
		{
			if (
				attribute.AttributeClass is { } attributeClass
				&& HasMetadataName(attributeClass, ResultNamespace, GenerateResultAttributeMetadataName)
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

		HashSet<INamedTypeSymbol> visited = [with(SymbolEqualityComparer.Default)];

		return IsCaseOf(union, caseType, visited);
	}

	static bool IsCaseOf(INamedTypeSymbol union, ITypeSymbol caseType, HashSet<INamedTypeSymbol> visited)
	{
		// A case type that is itself a union is an included union: its own cases are reachable through the
		// union-receiver factory of the outer union too, so the rewrite binds for them as well. The visited
		// set keeps a recursive union graph from looping.
		if (!visited.Add(union))
			return false;

		foreach (var constructor in union.InstanceConstructors)
		{
			if (
				constructor.DeclaredAccessibility != Accessibility.Public
				|| constructor.Parameters.Length != 1
				|| constructor.Parameters[0].RefKind is not (RefKind.None or RefKind.In)
			)
				continue;

			var parameterType = constructor.Parameters[0].Type;

			if (SymbolEqualityComparer.Default.Equals(parameterType, caseType))
				return true;

			if (parameterType is INamedTypeSymbol { IsUnion: true } included && IsCaseOf(included, caseType, visited))
				return true;
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

	static async Task<Document> UseUnionFactoryAsync(
		Document document,
		ExpressionSyntax expression,
		INamedTypeSymbol unionType,
		ITypeSymbol? valueType,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		// The union-receiver factory is named on the union type: Union.Failure(case) for a unit result and
		// Union.Failure<TValue>(case) for a value result.
		var unionName = SyntaxFactory
			.ParseTypeName(unionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
			.WithAdditionalAnnotations(Simplifier.Annotation);

		SimpleNameSyntax factoryName;
		if (valueType is null)
		{
			factoryName = SyntaxFactory.IdentifierName(FailureFactoryName);
		}
		else
		{
			var typeArgument = SyntaxFactory
				.ParseTypeName(valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
				.WithAdditionalAnnotations(Simplifier.Annotation);

			factoryName = SyntaxFactory.GenericName(
				SyntaxFactory.Identifier(FailureFactoryName),
				SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArgument))
			);
		}

		var invocation = SyntaxFactory
			.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, unionName, factoryName),
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(expression.WithoutTrivia()))
				)
			)
			.WithTriviaFrom(expression)
			.WithAdditionalAnnotations(Formatter.Annotation);

		return document.WithSyntaxRoot(root.ReplaceNode(expression, invocation));
	}
}
