using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace Purview.Results.AspNetCore;

/// <summary>
/// Covers the metrics the HTTP mapper emits.
/// </summary>
/// <remarks>
/// The design treats an unmapped failure as a host mapping gap rather than a domain outcome, which is only
/// actionable if it can be counted. Previously the only signal was a log message, so answering "how often,
/// and for which error" meant scraping log text.
/// </remarks>
public class ResultsHttpTelemetryTests
{
	[Test]
	public async Task Map_GivenUnmappedFailure_CountsIt()
	{
		// Arrange
		using MeterListener listener = new();
		var measurements = Listen(listener, "purview.results.failures.unmapped");

		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		_ = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert — filtered by the error-type tag rather than counted outright. The Meter is process-static
		// and other tests in this assembly exercise the same counters concurrently, so an exact total would
		// be flaky.
		var mine = ForErrorType(measurements, typeof(ItemNotFound));

		await Assert.That(mine.Count).IsGreaterThanOrEqualTo(1);
		await Assert.That(mine[0].Value).IsEqualTo(1L);
	}

	[Test]
	public async Task Map_GivenUnmappedFailure_TagsTheMeasurementWithTheErrorType()
	{
		// Arrange — the type name is the dimension that makes the count actionable. Metrics stay inside the
		// host's own infrastructure, so unlike the HTTP response it is always included.
		using MeterListener listener = new();
		var measurements = Listen(listener, "purview.results.failures.unmapped");

		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		_ = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert
		var tag = measurements[0].Tags.Single(static pair => pair.Key == ResultsHttpTelemetry.ErrorTypeTag);

		await Assert.That(tag.Value).IsEqualTo(typeof(ItemNotFound).FullName);
	}

	[Test]
	public async Task Map_GivenMappedFailure_CountsItAsMappedNotUnmapped()
	{
		// Arrange
		using MeterListener listener = new();
		// One Listen per listener: it registers the callback and starts the listener, so calling it twice
		// overwrites the first subscription.
		var mapped = Listen(listener, "purview.results.failures.mapped");

		var mapper = ResultsHttpTestFactory.CreateMapper(static options =>
			options.Map<ItemNotFound>(static _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound))
		);
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		var response = await ResultsHttpTestFactory.ExecuteAsync(
			mapper.Map(new ItemNotFound(7).AsFailure<int>(), context),
			context
		);

		// Assert — the 404 proves the mapped path ran, and the mapped counter recorded it against this
		// error type. The unmapped counter is not asserted here: the counters are process-static and a
		// sibling test deliberately produces an unmapped ItemNotFound, so it cannot be isolated.
		await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
		await Assert.That(ForErrorType(mapped, typeof(ItemNotFound)).Count).IsGreaterThanOrEqualTo(1);
	}

	[Test]
	public async Task Map_GivenUninitializedResult_CountsIt()
	{
		// Arrange — an endpoint returning default is always a bug, so it is counted separately.
		using MeterListener listener = new();
		var measurements = Listen(listener, "purview.results.uninitialized");

		var mapper = ResultsHttpTestFactory.CreateMapper();
		var context = ResultsHttpTestFactory.CreateContext();

		// Act
		_ = await ResultsHttpTestFactory.ExecuteAsync(mapper.Map(default(Result<int, ItemNotFound>), context), context);

		// Assert — the uninitialized counter carries no error type, so this one is only tolerant of extras.
		await Assert.That(measurements.Count).IsGreaterThanOrEqualTo(1);
	}

	[Test]
	public async Task MeterName_IsStable()
	{
		// Consumers register this name with their metrics pipeline, so it is public contract.
		await Assert.That(ResultsHttpTelemetry.MeterName).IsEqualTo("Purview.Results.AspNetCore");
		await Assert.That(ResultsHttpTelemetry.ErrorTypeTag).IsEqualTo("purview.results.error_type");
	}

	/// <summary>
	/// The measurements tagged with <paramref name="errorType"/>.
	/// </summary>
	static List<(long Value, KeyValuePair<string, object?>[] Tags)> ForErrorType(
		List<(long Value, KeyValuePair<string, object?>[] Tags)> measurements,
		Type errorType
	) =>
		[
			.. measurements.Where(measurement =>
				measurement.Tags.Any(tag =>
					tag.Key == ResultsHttpTelemetry.ErrorTypeTag && (string?)tag.Value == errorType.FullName
				)
			),
		];

	/// <summary>
	/// Subscribes to one instrument by name and collects its measurements.
	/// </summary>
	static List<(long Value, KeyValuePair<string, object?>[] Tags)> Listen(MeterListener listener, string instrument)
	{
		List<(long, KeyValuePair<string, object?>[])> measurements = [];

		listener.InstrumentPublished += (published, active) =>
		{
			if (published.Meter.Name == ResultsHttpTelemetry.MeterName && published.Name == instrument)
				active.EnableMeasurementEvents(published);
		};

		listener.SetMeasurementEventCallback<long>(
			(_, measurement, tags, _) => measurements.Add((measurement, tags.ToArray()))
		);

		listener.Start();

		return measurements;
	}
}
