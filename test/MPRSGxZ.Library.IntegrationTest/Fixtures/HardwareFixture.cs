using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.IntegrationTest.Configuration;
using MPRSGxZ.Library.Ports;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Hands out exclusive connections to the physical amplifier and routes library logs into the running test's output.
/// </summary>
/// <remarks>
/// The bridge in front of the amplifier accepts a single TCP client, so access has to be serialised. That is enforced
/// twice over: the collection definition stops xUnit running two hardware tests concurrently, and
/// <see cref="OpenSessionAsync"/> holds a semaphore for the life of each session so a stray parallel caller waits
/// rather than being refused by the bridge.
///
/// Each test gets a fresh connection rather than sharing one. Connection setup is itself part of what these tests
/// exercise, and reusing a socket would let one test's leftover state, such as a negotiated baud rate or undrained
/// bytes, decide whether the next one passes.
/// </remarks>
public sealed class HardwareFixture : IAsyncLifetime, ITestOutputHelperAccessor
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly ILoggerFactory _loggerFactory;

	/// <summary>
	/// Gets the settings describing the rig under test.
	/// </summary>
	public HardwareTestOptions Options { get; } = HardwareTestConfiguration.Options;

	/// <summary>
	/// Gets or sets the output helper for the currently executing test.
	/// </summary>
	/// <remarks>
	/// The fixture outlives every individual test, so its logger factory cannot be bound to one test's output helper.
	/// Instead the accessor is reassigned as each test starts and the logging provider resolves it per message.
	/// </remarks>
	public ITestOutputHelper? OutputHelper { get; set; }

	public HardwareFixture()
	{
		_loggerFactory = LoggerFactory.Create(builder => builder
			.SetMinimumLevel(LogLevel.Trace)
			.AddXUnit(this));
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Creates a logger that writes to the currently executing test's output.
	/// </summary>
	public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

	/// <summary>
	/// Opens an exclusive connection to the amplifier.
	/// </summary>
	/// <remarks>
	/// Fails the test if the connection cannot be established. Callers are expected to be gated behind
	/// <see cref="HardwareFactAttribute"/>, so reaching this method at all means hardware was promised.
	/// </remarks>
	/// <returns>A session that must be disposed to release the connection.</returns>
	internal async Task<HardwareSession> OpenSessionAsync(CancellationToken token)
	{
		Assert.SkipWhen(Options.GetSkipReason() is not null, Options.GetSkipReason() ?? string.Empty);

		await _gate.WaitAsync(token);

		IPAmplifierPort port;
		ConnectionState state;

		try
		{
			port = CreatePort();
			state = await port.OpenAsync(token);
		}
		catch
		{
			_gate.Release();
			throw;
		}

		if (state != ConnectionState.Connected)
		{
			await port.DisposeAsync();
			_gate.Release();

			Assert.Fail($"Could not connect to the amplifier at {Options.Host}:{Options.Port}. Connection state was {state}.");
		}

		return new HardwareSession(port, Options, _gate);
	}

	/// <summary>
	/// Creates an unopened port pointed at the configured endpoint.
	/// </summary>
	/// <remarks>
	/// Exposed separately from <see cref="OpenSessionAsync"/> so tests can assert on connection behaviour itself,
	/// including the failure paths, without the fixture having already decided the outcome.
	/// </remarks>
	internal IPAmplifierPort CreatePort()
		=> new(CreateLogger<IPAmplifierPort>(), new StaticEndpointProvider(Options.GetEndpoint()));

	public ValueTask DisposeAsync()
	{
		_loggerFactory.Dispose();
		_gate.Dispose();

		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// Groups every hardware test into a single collection.
/// </summary>
/// <remarks>
/// xUnit runs separate collections in parallel but the tests within one collection sequentially. Putting all hardware
/// tests in one collection with parallelisation disabled is what keeps them from contending for the single physical
/// link.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HardwareCollection : ICollectionFixture<HardwareFixture>
{
	public const string Name = "Hardware";
}
