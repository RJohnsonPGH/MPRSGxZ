using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Captures every settable property of one zone so a test that writes to it can put it back.
/// </summary>
/// <remarks>
/// These tests run against equipment in someone's house. A test that leaves a zone powered on at volume 38 is worse
/// than a test that fails, so every mutating test brackets its work in a snapshot and restores in a <c>finally</c>.
///
/// Restore is deliberately best effort and never throws: a restore failure during the unwind of an already failing
/// test would replace the real assertion message with a less useful one. Failures are surfaced through the test's log
/// output instead.
/// </remarks>
internal sealed class ZoneStateSnapshot
{
	private readonly AmplifierIdentifier _ampId;
	private readonly ZoneIdentifier _zoneId;
	private readonly Dictionary<SetCommandType, int> _values;

	private ZoneStateSnapshot(AmplifierIdentifier ampId, ZoneIdentifier zoneId, Dictionary<SetCommandType, int> values)
	{
		_ampId = ampId;
		_zoneId = zoneId;
		_values = values;
	}

	/// <summary>
	/// Reads the current state of a zone.
	/// </summary>
	public static async Task<ZoneStateSnapshot> CaptureAsync(
		HardwareSession session,
		AmplifierIdentifier ampId,
		ZoneIdentifier zoneId,
		CancellationToken token)
	{
		var response = await session.QueryZoneAsync(ampId, zoneId, token);

		return new ZoneStateSnapshot(ampId, zoneId, Read(response));
	}

	/// <summary>
	/// Gets the captured value of a property.
	/// </summary>
	public int this[SetCommandType commandType] => _values[commandType];

	/// <summary>
	/// Writes every captured value back to the zone.
	/// </summary>
	/// <remarks>
	/// Restores unconditionally rather than only the properties that differ. A test may have failed midway through
	/// its own writes, so what the zone currently reports cannot be trusted to identify what needs undoing.
	///
	/// Power is restored last: turning a zone off resets some of the other properties on the hardware, so writing it
	/// first would discard the values that follow.
	/// </remarks>
	public async Task RestoreAsync(HardwareSession session, CancellationToken token)
	{
		var ordered = _values.Keys
			.OrderBy(x => x == SetCommandType.Power)
			.ToList();

		foreach (var commandType in ordered)
		{
			try
			{
				await session.Port.ExecuteCommandAsync(
					SetCommand.Create(_ampId, _zoneId, commandType, _values[commandType]),
					token);
			}
			catch (Exception ex)
			{
				TestContext.Current.TestOutputHelper?.WriteLine(
					$"Failed to restore {commandType} on amplifier {(int)_ampId} zone {(int)_zoneId}: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Asserts that the zone currently matches this snapshot.
	/// </summary>
	public async Task AssertMatchesAsync(HardwareSession session, CancellationToken token)
	{
		var response = await session.QueryZoneAsync(_ampId, _zoneId, token);

		Assert.Equal(_values, Read(response));
	}

	private static Dictionary<SetCommandType, int> Read(CommandResponse response)
		=> new()
		{
			[SetCommandType.Power] = response.Power ? 1 : 0,
			[SetCommandType.Mute] = response.Mute ? 1 : 0,
			[SetCommandType.DoNotDisturb] = response.DoNotDisturb ? 1 : 0,
			[SetCommandType.Volume] = response.Volume,
			[SetCommandType.Treble] = response.Treble,
			[SetCommandType.Bass] = response.Bass,
			[SetCommandType.Balance] = response.Balance,
			[SetCommandType.Source] = response.Source
		};
}
