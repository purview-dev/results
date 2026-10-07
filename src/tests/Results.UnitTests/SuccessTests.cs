namespace Purview.Results;

public sealed class SuccessTests
{
	[Test]
	public async Task Instance_ShouldEqualDefault()
	{
		await Assert.That(Success.Instance).IsEqualTo(default);
	}

	[Test]
	public async Task ToString_ShouldDescribeSuccess()
	{
		await Assert.That(Success.Instance.ToString()).IsEqualTo("Success");
	}
}
