using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.IntegrationTest.Configuration;
using MPRSGxZ.Library.Ports;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// An open, exclusively owned connection to the amplifier for the duration of a single test.
/// </summary>
/// <remarks>
/// Obtained from <see cref="HardwareFixture.OpenSessionAsync"/> and disposed by the test. Disposal closes the socket,
/// which is what releases the bridge for the next test.
/// </remarks>
internal sealed class HardwareSession(IPAmplifierPort port, HardwareTestOptions options, SemaphoreSlim gate) : IAsyncDisposable
{
	private bool _disposed;

	/// <summary>
	/// Gets the open port.
	/// </summary>
	public IPAmplifierPort Port { get; } = port;

	/// <summary>
	/// Gets the settings describing the rig.
	/// </summary>
	public HardwareTestOptions Options { get; } = options;

	/// <summary>
	/// Executes a command, failing the test rather than returning a failed result if the exchange does not complete.
	/// </summary>
	/// <remarks>
	/// Most tests treat a failed exchange as a test failure, and the raw <see cref="CommandResult"/> carries no
	/// diagnostic detail beyond its status, so the assertion is centralised here with the command text attached.
	/// </remarks>
	public async Task<CommandResult> ExecuteAsync(ICommand command, CancellationToken token)
	{
		var result = await Port.ExecuteCommandAsync(command, token);

		Assert.Equal(CommandResultStatus.Success, result.Status);

		return result;
	}

	/// <summary>
	/// Queries a single zone and returns its decoded status.
	/// </summary>
	public async Task<CommandResponse> QueryZoneAsync(AmplifierIdentifier ampId, ZoneIdentifier zoneId, CancellationToken token)
	{
		var result = await ExecuteAsync(new QueryCommand(ampId, zoneId), token);

		return Assert.Single(result.Response);
	}

	/// <summary>
	/// Writes a value, waits for the amplifier to apply it, and returns the zone's status as read back afterwards.
	/// </summary>
	/// <remarks>
	/// The amplifier acknowledges a set command before it has finished applying it, so an immediate read can still
	/// report the previous value. The settle delay is configurable because how long that takes varies with the
	/// property and the link speed.
	/// </remarks>
	public async Task<CommandResponse> SetAndReadBackAsync(
		AmplifierIdentifier ampId,
		ZoneIdentifier zoneId,
		SetCommandType commandType,
		int value,
		CancellationToken token)
	{
		await ExecuteAsync(SetCommand.Create(ampId, zoneId, commandType, value), token);
		await Task.Delay(Options.SettleDelay, token);

		return await QueryZoneAsync(ampId, zoneId, token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			await Port.CloseAsync();
			await Port.DisposeAsync();
		}
		finally
		{
			// Released last, and unconditionally: a failure while closing must not strand the gate and wedge every
			// subsequent test behind it.
			gate.Release();
		}
	}
}
