using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Ports;
using MPRSGxZ.Library.Providers;

namespace MPRSGxZ.Library.Test;

/// <summary>
/// Covers closing and reopening a port around a connection that was never established.
/// </summary>
/// <remarks>
/// These need no amplifier because they only exercise the paths taken when connecting fails, which is reached by
/// pointing the port at an endpoint nothing is listening on. That is the state the connection settings page starts
/// in, and the state it returns to whenever the address is edited to something wrong.
/// </remarks>
[TestClass]
public sealed class PortLifecycleTests
{
	/// <summary>
	/// Closing a port whose connection was never established must not throw.
	/// </summary>
	/// <remarks>
	/// Reconnecting closes before it opens, so this is reached whenever the address is changed while the current
	/// address is unreachable. The stream is only assigned on a successful connect, so closing here previously
	/// dereferenced null and left the connection settings page unable to accept a corrected address.
	/// </remarks>
	[TestMethod]
	public async Task CloseAsync_AfterFailedOpen_DoesNotThrow()
	{
		await using var port = CreatePort();

		Assert.AreEqual(ConnectionState.ErrorInvalidAddress, await port.OpenAsync(CancellationToken.None));

		await port.CloseAsync();
	}

	/// <summary>
	/// Closing a port that was never opened at all must not throw.
	/// </summary>
	/// <remarks>
	/// A consumer that closes defensively during shutdown cannot know whether opening was ever attempted.
	/// </remarks>
	[TestMethod]
	public async Task CloseAsync_WithoutOpening_DoesNotThrow()
	{
		await using var port = CreatePort();

		await port.CloseAsync();
	}

	/// <summary>
	/// Closing twice must not throw.
	/// </summary>
	/// <remarks>
	/// The second close would otherwise dispose a stream belonging to a connection that is already gone.
	/// </remarks>
	[TestMethod]
	public async Task CloseAsync_CalledTwice_DoesNotThrow()
	{
		await using var port = CreatePort();

		await port.OpenAsync(CancellationToken.None);

		await port.CloseAsync();
		await port.CloseAsync();
	}

	/// <summary>
	/// Repeatedly failing to open must leave the port reusable rather than accumulating sockets.
	/// </summary>
	/// <remarks>
	/// Every attempt replaces the client, so the previous one has to be released. This is the shape of a user
	/// correcting a mistyped address a few times, and each attempt must report the same failure rather than
	/// degrading into a different one.
	/// </remarks>
	[TestMethod]
	public async Task OpenAsync_RepeatedFailures_KeepReportingTheSameFailure()
	{
		await using var port = CreatePort();

		for (var attempt = 0; attempt < 5; attempt++)
		{
			Assert.AreEqual(ConnectionState.ErrorInvalidAddress, await port.OpenAsync(CancellationToken.None));
			await port.CloseAsync();
		}
	}

	/// <summary>
	/// Executing a command without a connection must surface a link failure rather than a
	/// <see cref="NullReferenceException"/>.
	/// </summary>
	/// <remarks>
	/// Callers classify link faults by exception type, so a null dereference would slip past the handlers that turn a
	/// dead link into a reported failure and a reconnect, and would instead escape as an unhandled fault.
	/// </remarks>
	[TestMethod]
	public async Task ExecuteCommandAsync_WithoutAConnection_ThrowsALinkFailure()
	{
		await using var port = CreatePort();

		await Assert.ThrowsExactlyAsync<IOException>(() => port.ExecuteCommandAsync(
			new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
			CancellationToken.None));
	}

	/// <summary>
	/// Executing a command without a connection must fail without spending the retry budget.
	/// </summary>
	/// <remarks>
	/// Retrying is meant for a link that faults mid-command, and is pointless when there is no connection to write to.
	/// Left to the retry policy this would consume the caller's whole timeout on attempts that cannot reach the wire,
	/// which on a slow link is measured in seconds per command.
	/// </remarks>
	[TestMethod]
	public async Task ExecuteCommandAsync_WithoutAConnection_FailsWithoutRetrying()
	{
		await using var port = CreatePort();
		var started = DateTime.UtcNow;

		await Assert.ThrowsExactlyAsync<IOException>(() => port.ExecuteCommandAsync(
			new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
			CancellationToken.None));

		// The policy backs off 50ms, 100ms then 200ms, so anything under that floor cannot have retried.
		Assert.IsLessThan(TimeSpan.FromMilliseconds(350), DateTime.UtcNow - started);
	}

	/// <summary>
	/// Counting amplifiers without a connection must surface a link failure rather than a
	/// <see cref="NullReferenceException"/>.
	/// </summary>
	[TestMethod]
	public async Task GetAmplifierCountAsync_WithoutAConnection_ThrowsALinkFailure()
	{
		await using var port = CreatePort();

		await Assert.ThrowsExactlyAsync<IOException>(() => port.GetAmplifierCountAsync(CancellationToken.None));
	}

	/// <summary>
	/// A command issued after closing must surface a link failure rather than reusing the disposed connection.
	/// </summary>
	/// <remarks>
	/// Closing clears the stream, so this covers the window between a connection being torn down and its replacement
	/// being established, which is where a reconnect spends its time.
	/// </remarks>
	[TestMethod]
	public async Task ExecuteCommandAsync_AfterClosing_ThrowsALinkFailure()
	{
		await using var port = CreatePort();

		await port.OpenAsync(CancellationToken.None);
		await port.CloseAsync();

		await Assert.ThrowsExactlyAsync<IOException>(() => port.ExecuteCommandAsync(
			new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
			CancellationToken.None));
	}

	/// <summary>
	/// Disposing after a failed open must not throw.
	/// </summary>
	[TestMethod]
	public async Task DisposeAsync_AfterFailedOpen_DoesNotThrow()
	{
		var port = CreatePort();

		await port.OpenAsync(CancellationToken.None);
		await port.DisposeAsync();
	}

	private static IPAmplifierPort CreatePort()
		=> new(NullLogger<IPAmplifierPort>.Instance, new FixedEndpointProvider(UnusedLoopbackEndpoint()));

	/// <summary>
	/// Gets a loopback endpoint that nothing is listening on.
	/// </summary>
	/// <remarks>
	/// A port is bound only to discover a free number and is released immediately, so connecting to it is refused
	/// promptly. This keeps the tests offline and fast rather than waiting out a connect timeout to an address that
	/// merely happens to be unroutable.
	/// </remarks>
	private static IPEndPoint UnusedLoopbackEndpoint()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var endpoint = (IPEndPoint)listener.LocalEndpoint;
		listener.Stop();

		return endpoint;
	}

	private sealed class FixedEndpointProvider(IPEndPoint endpoint) : IEndpointProvider
	{
		public IPEndPoint Endpoint => endpoint;

		public Task RefreshAsync(CancellationToken token = default) => Task.CompletedTask;
	}
}
