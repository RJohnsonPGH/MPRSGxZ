using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.IntegrationTest.Configuration;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Base class for tests that talk to the physical amplifier.
/// </summary>
/// <remarks>
/// Exists to do three things every hardware test needs: point the fixture's logging at this test's output, bound the
/// test with a timeout so a wedged bridge cannot hang the run indefinitely, and expose the session factory.
/// </remarks>
[Collection(HardwareCollection.Name)]
public abstract class HardwareTestBase : IDisposable
{
	private readonly HardwareFixture _fixture;
	private readonly CancellationTokenSource _timeout;
	private readonly Lazy<CancellationTokenSource> _cleanup;

	/// <summary>
	/// Gets the settings describing the rig under test.
	/// </summary>
	protected HardwareTestOptions Options => _fixture.Options;

	/// <summary>
	/// Gets a token that is cancelled once the configured per test timeout elapses.
	/// </summary>
	/// <remarks>
	/// A hardware test that stops making progress typically does so by blocking on a read that will never complete,
	/// which no assertion can rescue it from. Every await in these tests takes this token so the test fails with a
	/// cancellation instead of hanging the suite.
	/// </remarks>
	protected CancellationToken Token => _timeout.Token;

	/// <summary>
	/// Gets a token for cleanup work, with its own budget independent of <see cref="Token"/>.
	/// </summary>
	/// <remarks>
	/// A test that times out has already cancelled <see cref="Token"/>, so a <c>finally</c> block that reused it could
	/// never restore the hardware and would throw a cancellation that replaces the real assertion message. Cleanup
	/// therefore gets a fresh budget, which is what makes a mutating test's restore reliable on the failure path.
	///
	/// The budget starts when cleanup first asks for the token rather than when the test starts, so a test that runs
	/// its timeout to the very end still gets the full allowance to put the hardware back.
	/// </remarks>
	protected CancellationToken CleanupToken => _cleanup.Value.Token;

	/// <summary>
	/// Gets this test's output, for recording observed hardware values that are useful when diagnosing a failure but
	/// too rig specific to assert on.
	/// </summary>
	protected ITestOutputHelper Output { get; }

	protected HardwareTestBase(HardwareFixture fixture, ITestOutputHelper outputHelper)
	{
		_fixture = fixture;
		Output = outputHelper;

		// Tests in this collection run sequentially, so reassigning the shared accessor cannot race with another test.
		_fixture.OutputHelper = outputHelper;

		_timeout = new CancellationTokenSource(fixture.Options.Timeout);
		_cleanup = new Lazy<CancellationTokenSource>(() => new CancellationTokenSource(fixture.Options.Timeout));
	}

	/// <summary>
	/// Opens an exclusive connection to the amplifier. Dispose the result to release it.
	/// </summary>
	internal Task<HardwareSession> OpenSessionAsync() => _fixture.OpenSessionAsync(Token);

	/// <summary>
	/// Opens an exclusive connection for cleanup work, using <see cref="CleanupToken"/>.
	/// </summary>
	internal Task<HardwareSession> OpenCleanupSessionAsync() => _fixture.OpenSessionAsync(CleanupToken);

	/// <summary>
	/// Creates an unopened port pointed at the configured endpoint.
	/// </summary>
	internal Library.Ports.IPAmplifierPort CreatePort() => _fixture.CreatePort();

	/// <summary>
	/// Creates a logger that writes to this test's output.
	/// </summary>
	protected ILogger<T> CreateLogger<T>() => _fixture.CreateLogger<T>();

	public void Dispose()
	{
		_timeout.Dispose();
		if (_cleanup.IsValueCreated)
		{
			_cleanup.Value.Dispose();
		}
		_fixture.OutputHelper = null;

		GC.SuppressFinalize(this);
	}
}
