using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.IntegrationTest.Fixtures;
using MPRSGxZ.Library.Providers;
using System.Net;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers opening, closing and reopening the link, including how failures are reported.
/// </summary>
public sealed class ConnectionTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	[HardwareFact]
	public async Task OpenAsync_ReportsConnected()
	{
		await using var port = CreatePort();

		var state = await port.OpenAsync(Token);

		Assert.Equal(ConnectionState.Connected, state);
		Assert.Equal(ConnectionState.Connected, port.ConnectionState);
	}

	/// <summary>
	/// The port must survive a close and reopen on the same instance.
	/// </summary>
	/// <remarks>
	/// A <see cref="System.Net.Sockets.TcpClient"/> cannot be reconnected once closed, so the port replaces it on
	/// every open. This is the behaviour the reconnect path in <see cref="Amplifier"/> depends on, and it is easy to
	/// regress by hoisting the client into a field initialiser and never rebuilding it.
	/// </remarks>
	[HardwareFact]
	public async Task OpenAsync_AfterClose_Reconnects()
	{
		await using var port = CreatePort();

		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));
		await port.CloseAsync();

		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));

		// A command after the reopen proves the link is genuinely usable, not merely reporting a connected state.
		var count = await port.GetAmplifierCountAsync(Token);
		Assert.Equal(Options.ExpectedAmplifierCount, count);
	}

	/// <summary>
	/// Baud negotiation must settle on a rate that the command timeout is derived from.
	/// </summary>
	/// <remarks>
	/// The negotiated rate is not exposed, so it is inferred from the resulting timeout. The assertion is deliberately
	/// loose: it only requires the timeout to fall inside the range the supported rates can produce, which catches the
	/// real failure mode of the field never being updated and the link running on the default.
	/// </remarks>
	[HardwareFact]
	public async Task OpenAsync_DerivesCommandTimeoutFromNegotiatedBaud()
	{
		await using var port = CreatePort();

		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));

		var rates = Enum.GetValues<Baud>();
		var fastest = Ports.BaudTiming.GetCommandTimeout(rates[^1]);
		var slowest = Ports.BaudTiming.GetCommandTimeout(rates[0]);

		Output.WriteLine($"Command timeout is {port.CommandTimeout.TotalMilliseconds} ms.");

		Assert.InRange(port.CommandTimeout, fastest, slowest);
	}

	/// <summary>
	/// Polling must never be scheduled faster than a full round of queries can complete.
	/// </summary>
	/// <remarks>
	/// At the slowest supported rate a single query costs several hundred milliseconds, so a fixed 250 ms poll
	/// interval would queue work faster than the link can drain it and every command would eventually time out
	/// waiting for the semaphore.
	/// </remarks>
	[HardwareFact]
	public async Task GetPollInterval_IsAtLeastOneTimeoutPerAmplifier()
	{
		await using var port = CreatePort();

		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));

		var amplifierCount = Options.ExpectedAmplifierCount;
		var interval = port.GetPollInterval(amplifierCount, TimeSpan.FromMilliseconds(250));

		Assert.True(
			interval >= port.CommandTimeout * amplifierCount,
			$"A poll interval of {interval.TotalMilliseconds} ms cannot accommodate {amplifierCount} query/queries of {port.CommandTimeout.TotalMilliseconds} ms each.");
	}

	/// <summary>
	/// An unreachable endpoint must be reported, not thrown.
	/// </summary>
	/// <remarks>
	/// Uses the discard address 198.51.100.0 from the TEST-NET-2 block, which is reserved for documentation and is
	/// therefore guaranteed not to host a real amplifier. Connecting is expected to fail fast or to be abandoned by
	/// the test timeout; either way the port must return a state rather than propagate a socket exception, because
	/// callers treat the state as the connection outcome.
	/// </remarks>
	[Fact]
	public async Task OpenAsync_WithUnreachableEndpoint_ReportsError()
	{
		var endpoint = new IPEndPoint(IPAddress.Parse("198.51.100.0"), 23);

		await using var port = new Ports.IPAmplifierPort(
			CreateLogger<Ports.IPAmplifierPort>(),
			new StaticEndpointProvider(endpoint));

		using var attempt = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		var state = await port.OpenAsync(attempt.Token);

		Assert.Equal(ConnectionState.ErrorInvalidAddress, state);
	}

	/// <summary>
	/// The endpoint provider is consulted on every open rather than captured once.
	/// </summary>
	/// <remarks>
	/// This is what allows a bridge whose address has changed to be picked up by a reconnect. The first open targets
	/// an address that cannot answer and the second targets the real rig, using one provider throughout, so a port
	/// that cached the endpoint would fail the second open.
	/// </remarks>
	[HardwareFact]
	public async Task OpenAsync_ReReadsEndpointFromProvider()
	{
		var provider = new MutableEndpointProvider(new IPEndPoint(IPAddress.Parse("198.51.100.0"), 23));

		await using var port = new Ports.IPAmplifierPort(CreateLogger<Ports.IPAmplifierPort>(), provider);

		using var firstAttempt = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		Assert.Equal(ConnectionState.ErrorInvalidAddress, await port.OpenAsync(firstAttempt.Token));

		provider.Endpoint = Options.GetEndpoint();

		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));
	}

	private sealed class MutableEndpointProvider(IPEndPoint endpoint) : IEndpointProvider
	{
		public IPEndPoint Endpoint { get; set; } = endpoint;

		public Task RefreshAsync(CancellationToken token = default) => Task.CompletedTask;
	}
}
