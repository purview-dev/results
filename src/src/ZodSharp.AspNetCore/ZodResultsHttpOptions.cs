using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Purview.Results.ZodSharp.AspNetCore;

/// <summary>
/// Configures how a result failure that carries ZodSharp validation errors is rendered, by validation error
/// <em>code</em>, by <em>category</em> and by <em>origin</em>.
/// </summary>
/// <remarks>
/// <para>
/// Without a rule, a failure that carries validation errors is rendered as one <c>HttpValidationProblemDetails</c>,
/// produced by the same mapping a thrown <c>ZodException</c> uses. A rule answers a failure whose errors include
/// its code (or category, or origin) with a response of its own, which is what lets one error code be a plain
/// <c>404</c> while the rest stay validation problems.
/// </para>
/// <para>
/// The three axes are the identity a <see cref="ValidationError"/> carries: <see cref="ValidationError.Code"/> is
/// the specific rule that failed, <see cref="ValidationError.Category"/> is a broad grouping many codes share, and
/// <see cref="ValidationError.Origin"/> is the structured origin a rule owns (<c>"value_object"</c> for a
/// type-level rule, <c>"array"</c> for a collection rule, and so on). A code, a category and an origin can each
/// answer one failure, so a rule that must key off something other than a code — a whole family of rules sharing
/// an origin — is expressible here.
/// </para>
/// <para>
/// Rules are matched against the failure's whole error set, in this order: every <b>code</b> rule in registration
/// order, then every <b>category</b> rule in registration order, then every <b>origin</b> rule in registration
/// order, then the default validation problem. A rule therefore applies when <em>any</em> of the failure's errors
/// carries its code, category or origin, so registering a code rule guarantees it is reachable whatever else the
/// schema reported. A factory that wants stricter semantics returns <see langword="null"/> to decline the failure,
/// and matching continues. The order is fixed rather than registration-ordered across axes, because a code is
/// narrower than a category, which is narrower than an origin: a rule can only narrow what the host already gets.
/// </para>
/// <para>
/// A factory is handed the failure's full error set, so a rule never hides the other problems the caller has to
/// fix. Register one rule per code, category or origin; registering the same value twice with different behaviour
/// is rejected at configuration time.
/// </para>
/// </remarks>
public sealed class ZodResultsHttpOptions
{
	readonly List<ZodErrorRule> _codeRules = [];
	readonly List<ZodErrorRule> _categoryRules = [];
	readonly List<ZodErrorRule> _originRules = [];

	/// <summary>
	/// Renders a validation failure whose errors include <paramref name="code"/> as the standard validation
	/// problem with a different default status code.
	/// </summary>
	/// <param name="code">The validation error code the rule applies to.</param>
	/// <param name="statusCode">
	/// The status code used when <see cref="ZodProblemDetailsOptions.StatusCodeSelector"/> and the resolved error
	/// type do not define one.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="code"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	public ZodResultsHttpOptions MapCode(string code, int statusCode) => AddCode(code, new(statusCode, null));

	/// <summary>
	/// Renders a validation failure whose errors include <paramref name="code"/> with a response of your own.
	/// </summary>
	/// <param name="code">The validation error code the rule applies to.</param>
	/// <param name="map">
	/// Creates the response from the failure's validation errors, or returns <see langword="null"/> to decline the
	/// failure and let matching continue.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="code"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	public ZodResultsHttpOptions MapCode(string code, Func<ImmutableArray<ValidationError>, HttpContext, IResult?> map)
	{
		ArgumentNullException.ThrowIfNull(map);

		return AddCode(code, new(null, map));
	}

	/// <summary>
	/// Renders a validation failure whose errors include the <paramref name="category"/> as the standard
	/// validation problem with a different default status code.
	/// </summary>
	/// <param name="category">The validation error category the rule applies to.</param>
	/// <param name="statusCode">
	/// The status code used when <see cref="ZodProblemDetailsOptions.StatusCodeSelector"/> and the resolved error
	/// type do not define one.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="category"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	public ZodResultsHttpOptions MapCategory(string category, int statusCode) =>
		AddCategory(category, new(statusCode, null));

	/// <summary>
	/// Renders a validation failure whose errors include the <paramref name="category"/> with a response of your
	/// own.
	/// </summary>
	/// <param name="category">The validation error category the rule applies to.</param>
	/// <param name="map">
	/// Creates the response from the failure's validation errors, or returns <see langword="null"/> to decline the
	/// failure and let matching continue.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="category"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	public ZodResultsHttpOptions MapCategory(
		string category,
		Func<ImmutableArray<ValidationError>, HttpContext, IResult?> map
	)
	{
		ArgumentNullException.ThrowIfNull(map);

		return AddCategory(category, new(null, map));
	}

	/// <summary>
	/// Renders a validation failure whose errors include the <paramref name="origin"/> as the standard validation
	/// problem with a different default status code.
	/// </summary>
	/// <param name="origin">
	/// The <see cref="ValidationError.Origin"/> the rule applies to — the origin a rule owns, such as
	/// <c>"value_object"</c> for a type-level rule or <c>"array"</c> for a collection rule.
	/// </param>
	/// <param name="statusCode">
	/// The status code used when <see cref="ZodProblemDetailsOptions.StatusCodeSelector"/> and the resolved error
	/// type do not define one.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="origin"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	public ZodResultsHttpOptions MapOrigin(string origin, int statusCode) => AddOrigin(origin, new(statusCode, null));

	/// <summary>
	/// Renders a validation failure whose errors include the <paramref name="origin"/> with a response of your own.
	/// </summary>
	/// <param name="origin">
	/// The <see cref="ValidationError.Origin"/> the rule applies to — the origin a rule owns, such as
	/// <c>"value_object"</c> for a type-level rule or <c>"array"</c> for a collection rule.
	/// </param>
	/// <param name="map">
	/// Creates the response from the failure's validation errors, or returns <see langword="null"/> to decline the
	/// failure and let matching continue.
	/// </param>
	/// <returns>The options instance for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="origin"/> is empty, or already has a rule with different behaviour.
	/// </exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is null.</exception>
	public ZodResultsHttpOptions MapOrigin(
		string origin,
		Func<ImmutableArray<ValidationError>, HttpContext, IResult?> map
	)
	{
		ArgumentNullException.ThrowIfNull(map);

		return AddOrigin(origin, new(null, map));
	}

	/// <summary>
	/// Finds the rules that could answer a failure, in the order they are consulted.
	/// </summary>
	/// <param name="errors">The validation errors the failure carries.</param>
	/// <returns>
	/// Every matching code rule in registration order, then every matching category rule, then every matching
	/// origin rule.
	/// </returns>
	internal IEnumerable<ZodErrorRule> Candidates(ImmutableArray<ValidationError> errors)
	{
		foreach (var rule in Matches(_codeRules, errors, static error => error.Code))
			yield return rule;

		foreach (var rule in Matches(_categoryRules, errors, static error => error.Category))
			yield return rule;

		foreach (var rule in Matches(_originRules, errors, static error => error.Origin))
			yield return rule;
	}

	ZodResultsHttpOptions AddCode(string code, ZodErrorRuleBehaviour behaviour) =>
		Add(_codeRules, code, "code", behaviour);

	ZodResultsHttpOptions AddCategory(string category, ZodErrorRuleBehaviour behaviour) =>
		Add(_categoryRules, category, "category", behaviour);

	ZodResultsHttpOptions AddOrigin(string origin, ZodErrorRuleBehaviour behaviour) =>
		Add(_originRules, origin, "origin", behaviour);

	ZodResultsHttpOptions Add(List<ZodErrorRule> rules, string value, string kind, ZodErrorRuleBehaviour behaviour)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		foreach (var rule in rules)
		{
			if (!string.Equals(rule.Value, value, StringComparison.Ordinal))
				continue;

			// The same rule twice is idempotent; two different answers for one value are ambiguous.
			if (IsSameBehaviour(rule.Behaviour, behaviour))
				return this;

			// The same value with different behaviour is a configuration error.
			throw new ArgumentException(
				$"The validation error {kind} '{value}' already has a mapping. Register one rule per {kind}.",
				nameof(value)
			);
		}

		rules.Add(new ZodErrorRule(value, behaviour));

		return this;
	}

	/// <summary>
	/// Answers whether two behaviours answer identically, comparing a factory by the method it binds rather than
	/// by delegate identity.
	/// </summary>
	/// <remarks>
	/// A delegate comparison would treat the same method group registered twice as different registrations —
	/// each occurrence allocates its own delegate — and so reject a configuration that answers one way. Two
	/// different lambdas compile to different methods, so they are still rejected.
	/// </remarks>
	static bool IsSameBehaviour(ZodErrorRuleBehaviour left, ZodErrorRuleBehaviour right)
	{
		if (left.StatusCode != right.StatusCode)
			return false;

		if (left.Map is null || right.Map is null)
			return left.Map is null && right.Map is null;

		// Compare the method and target of the factory, not the delegate itself, so the same method group is treated as the same behaviour.
		return left.Map.Target == right.Map.Target && left.Map.Method.Equals(right.Map.Method);
	}

	/// <summary>
	/// Finds the rules whose value any of the failure's errors carries, in registration order.
	/// </summary>
	static IEnumerable<ZodErrorRule> Matches(
		List<ZodErrorRule> rules,
		ImmutableArray<ValidationError> errors,
		Func<ValidationError, string?> select
	)
	{
		foreach (var rule in rules)
		{
			foreach (var error in errors)
			{
				if (!string.Equals(select(error), rule.Value, StringComparison.Ordinal))
					continue;

				yield return rule;
				break;
			}
		}
	}
}

/// <summary>How a code, category or origin rule answers the failures that mention it.</summary>
/// <param name="StatusCode">
/// The default status code of the validation problem, or <see langword="null"/> when the rule supplies a factory.
/// </param>
/// <param name="Map">
/// The factory that creates the response, or <see langword="null"/> when the rule renders a validation problem.
/// </param>
readonly record struct ZodErrorRuleBehaviour(
	int? StatusCode,
	Func<ImmutableArray<ValidationError>, HttpContext, IResult?>? Map
);

/// <summary>One registered code, category or origin rule.</summary>
/// <param name="Value">The error code, category or origin the rule applies to.</param>
/// <param name="Behaviour">How the rule answers a failure that mentions the value.</param>
sealed record ZodErrorRule(string Value, ZodErrorRuleBehaviour Behaviour);
