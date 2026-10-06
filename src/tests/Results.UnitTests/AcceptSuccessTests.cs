namespace Purview.Results;

/// <summary>
/// Pins the double-dispatch contract of <see cref="IResultValue.AcceptSuccess{TState, TReturn}"/>.
/// </summary>
/// <remarks>
/// <see cref="IResultValue.SuccessValue"/> erases the value to <see cref="object"/>. Infrastructure that has
/// to act on the value generically — serializing it — would then bind its generic parameter to
/// <see cref="object"/>, leaving a trimmed or Native AOT host with no type to resolve, and in ASP.NET Core
/// erasing the response type OpenAPI infers. These tests assert the real type arrives instead.
/// </remarks>
public class AcceptSuccessTests
{
	[Test]
	public async Task AcceptSuccess_GivenValueResult_VisitsWithTheDeclaredValueType()
	{
		// Arrange
		IResultValue result = Result.Success<int, string>(42);
		var visitor = new RecordingVisitor();

		// Act
		var visitedType = result.AcceptSuccess(visitor, 0);

		// Assert — int, not object. This is the whole point.
		await Assert.That(visitedType).IsEqualTo(typeof(int));
		await Assert.That(visitor.CapturedValue).IsEqualTo(42);
	}

	[Test]
	public async Task AcceptSuccess_GivenReferenceTypeValue_VisitsWithTheDeclaredValueType()
	{
		// Arrange — a reference type must not widen to object either.
		IResultValue result = Result.Success<string, int>("acme");
		var visitor = new RecordingVisitor();

		// Act
		var visitedType = result.AcceptSuccess(visitor, 0);

		// Assert
		await Assert.That(visitedType).IsEqualTo(typeof(string));
		await Assert.That(visitor.CapturedValue).IsEqualTo("acme");
	}

	[Test]
	public async Task AcceptSuccess_GivenUnitResult_VisitsWithTheSuccessMarker()
	{
		// Arrange — the unit result's value is the marker, which is how infrastructure tells a payload-free
		// success from one carrying a value.
		IResultValue result = Result.Success<string>();
		var visitor = new RecordingVisitor();

		// Act
		var visitedType = result.AcceptSuccess(visitor, 0);

		// Assert
		await Assert.That(visitedType).IsEqualTo(typeof(Success));
	}

	[Test]
	public async Task AcceptSuccess_PassesStateThroughWithoutCapturing()
	{
		// Arrange
		IResultValue result = Result.Success<int, string>(7);
		var visitor = new RecordingVisitor();

		// Act
		_ = result.AcceptSuccess(visitor, 99);

		// Assert
		await Assert.That(visitor.CapturedState).IsEqualTo(99);
	}

	[Test]
	public async Task AcceptSuccess_GivenNullVisitor_Throws()
	{
		IResultValue valueResult = Result.Success<int, string>(1);
		IResultValue unitResult = Result.Success<string>();

		await Assert.That(() => valueResult.AcceptSuccess<int, Type>(null!, 0)).Throws<ArgumentNullException>();
		await Assert.That(() => unitResult.AcceptSuccess<int, Type>(null!, 0)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task AcceptSuccess_GivenFailure_DoesNotThrow()
	{
		// Arrange — like every other IResultValue member this must never throw, so infrastructure can call it
		// before branching on IsSuccess.
		IResultValue result = Result.Failure<int, string>("nope");
		var visitor = new RecordingVisitor();

		// Act
		var visitedType = result.AcceptSuccess(visitor, 0);

		// Assert
		await Assert.That(visitedType).IsEqualTo(typeof(int));
		await Assert.That(visitor.CapturedValue).IsEqualTo(0);
	}

	sealed class RecordingVisitor : IResultValueVisitor<int, Type>
	{
		public object? CapturedValue { get; private set; }

		public int CapturedState { get; private set; }

		public Type VisitSuccess<TValue>(TValue value, int state)
		{
			CapturedValue = value;
			CapturedState = state;

			return typeof(TValue);
		}
	}
}
