using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Ports;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Reads hardware state over a port that an <see cref="Amplifier"/> is also using.
/// </summary>
/// <remarks>
/// Any test that writes through the facade has to confirm the result against the amplifier rather than against the
/// zone objects. A zone property setter updates its own backing field before the command is sent, so an assertion
/// that reads the zone back is satisfied the instant it is made and would pass with the hardware unplugged.
///
/// Sharing the amplifier's port is what makes that possible while the poll loop is running: the port serialises its
/// own exchanges, so a query issued here cannot interleave with a poll. Queries that are refused are reported as no
/// answer rather than asserted on, leaving the caller's wait loop to retry.
/// </remarks>
internal static class PortProbe
{
	/// <summary>
	/// Reads one zone, returning null if the exchange did not produce a single response.
	/// </summary>
	public static async Task<CommandResponse?> TryQueryZoneAsync(
		this IPAmplifierPort port,
		AmplifierIdentifier ampId,
		ZoneIdentifier zoneId,
		CancellationToken token)
	{
		var result = await port.ExecuteCommandAsync(new QueryCommand(ampId, zoneId), token);

		if (result.Status != CommandResultStatus.Success)
		{
			return null;
		}

		var responses = result.Response.ToList();

		return responses.Count == 1 ? responses[0] : null;
	}

	/// <summary>
	/// Reads every zone of an amplifier, keyed by its per-amplifier zone number.
	/// </summary>
	public static async Task<IReadOnlyDictionary<int, CommandResponse>?> TryQueryAmplifierAsync(
		this IPAmplifierPort port,
		AmplifierIdentifier ampId,
		CancellationToken token)
	{
		var result = await port.ExecuteCommandAsync(new QueryCommand(ampId, ZoneIdentifier.All), token);

		if (result.Status != CommandResultStatus.Success)
		{
			return null;
		}

		var responses = result.Response.ToList();

		return responses.Count == ZoneStateSnapshotSet.ZonesPerAmplifier
			? responses.ToDictionary(x => x.ZoneId)
			: null;
	}

	/// <summary>
	/// Reads the value a status response carries for a command type.
	/// </summary>
	public static int ValueOf(CommandResponse response, SetCommandType commandType)
	{
		if (commandType == SetCommandType.Power) return response.Power ? 1 : 0;
		if (commandType == SetCommandType.Mute) return response.Mute ? 1 : 0;
		if (commandType == SetCommandType.DoNotDisturb) return response.DoNotDisturb ? 1 : 0;
		if (commandType == SetCommandType.Volume) return response.Volume;
		if (commandType == SetCommandType.Treble) return response.Treble;
		if (commandType == SetCommandType.Bass) return response.Bass;
		if (commandType == SetCommandType.Balance) return response.Balance;
		if (commandType == SetCommandType.Source) return response.Source;

		throw new ArgumentOutOfRangeException(nameof(commandType), commandType.CommandString, "Not a reported property.");
	}
}
