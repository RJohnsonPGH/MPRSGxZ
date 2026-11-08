using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.IntegrationTest.Fixtures;
using MPRSGxZ.Library.Hardware;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers the write side of the protocol against the configured mutable zone.
/// </summary>
/// <remarks>
/// Every test here changes the state of real equipment, so they are gated behind the mutation opt in and every one of
/// them snapshots the zone first and restores it in a <c>finally</c>.
///
/// One zone is targeted throughout, chosen by the operator, so a test run cannot disturb a zone that someone is
/// listening to.
/// </remarks>
public sealed class SetCommandTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	/// <summary>
	/// Gets the amplifier and zone address of the configured mutable zone.
	/// </summary>
	/// <remarks>
	/// The setting is a flat zone id, matching how the library indexes zones, so it is translated here into the
	/// per amplifier addressing the protocol uses.
	/// </remarks>
	private AmplifierIdentifier AmpId => new HardwareZone(Options.MutableZoneId).AmpId;

	private ZoneIdentifier ZoneId => new HardwareZone(Options.MutableZoneId).ZoneId;

	/// <summary>
	/// A written value must be observable on a subsequent read.
	/// </summary>
	/// <remarks>
	/// Run for every settable property rather than one representative, because each has its own two letter mnemonic
	/// and range in the command table and a transposed pair only shows up on the property that owns it.
	///
	/// The target value is chosen away from both ends of the range so it cannot coincide with whatever the zone
	/// happened to be set to, which would let a set that did nothing at all pass.
	/// </remarks>
	[HardwareMutationTheory]
	[MemberData(nameof(SettableProperties))]
	public async Task SetCommand_IsAppliedAndReadBack(string commandTypeName)
	{
		var commandType = Resolve(commandTypeName);

		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			// Power must be on before the amplifier will accept or report changes to the audio properties.
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);

			var target = ChooseTarget(commandType, snapshot[commandType]);
			Output.WriteLine($"Setting {commandType.CommandString} to {target} (was {snapshot[commandType]}).");

			var response = await session.SetAndReadBackAsync(AmpId, ZoneId, commandType, target, Token);

			Assert.Equal(target, ValueOf(response, commandType));
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// Both ends of each property's declared range must be accepted by the hardware.
	/// </summary>
	/// <remarks>
	/// The ranges in the command table come from the specification sheet, and this is what confirms they match the
	/// firmware. A boundary the hardware rejects would otherwise be reported as a successful write whose read back
	/// silently disagrees.
	/// </remarks>
	[HardwareMutationTheory]
	[MemberData(nameof(SettableProperties))]
	public async Task SetCommand_AcceptsBothEndsOfItsRange(string commandTypeName)
	{
		var commandType = Resolve(commandTypeName);

		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);

			foreach (var value in new[] { commandType.MinValue, commandType.MaxValue })
			{
				// Power is the exception: writing its minimum turns the zone off, after which the amplifier stops
				// reporting changes, so it is exercised through its maximum only.
				if (commandType == SetCommandType.Power && value == commandType.MinValue)
				{
					continue;
				}

				var response = await session.SetAndReadBackAsync(AmpId, ZoneId, commandType, value, Token);

				Assert.Equal(value, ValueOf(response, commandType));
			}
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// Restoring a snapshot must return the zone to exactly the state it was captured in.
	/// </summary>
	/// <remarks>
	/// The restore path is what every other mutating test relies on to leave the rig untouched, so it is verified
	/// directly rather than trusted. If this test fails, the others cannot be considered safe to run.
	/// </remarks>
	[HardwareMutationFact]
	public async Task ZoneStateSnapshot_RestoresEveryProperty()
	{
		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);

			foreach (var commandType in SetCommandType.Settable.Where(x => x != SetCommandType.Power))
			{
				await session.ExecuteAsync(
					SetCommand.Create(AmpId, ZoneId, commandType, ChooseTarget(commandType, snapshot[commandType])),
					Token);
			}

			await Task.Delay(Options.SettleDelay, Token);
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}

		await Task.Delay(Options.SettleDelay, Token);
		await snapshot.AssertMatchesAsync(session, Token);
	}

	/// <summary>
	/// Setting a property that is mirrored across the amplifier must leave the other zones alone.
	/// </summary>
	/// <remarks>
	/// Source and volume are per zone on this hardware despite the shared source inputs, and a firmware or addressing
	/// mistake that applied a write to every zone would be both plausible and very noticeable to whoever owns the
	/// amplifier. Only the neighbouring zones on the same amplifier are checked, since that is where such a mistake
	/// would surface.
	/// </remarks>
	[HardwareMutationFact]
	public async Task SetCommand_DoesNotAffectOtherZones()
	{
		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		var before = await session.ExecuteAsync(new QueryCommand(AmpId, ZoneIdentifier.All), Token);
		var others = before.Response
			.Where(x => x.ZoneId != (int)ZoneId)
			.ToDictionary(x => x.ZoneId, x => x.Response);

		try
		{
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);

			var target = ChooseTarget(SetCommandType.Volume, snapshot[SetCommandType.Volume]);
			await session.SetAndReadBackAsync(AmpId, ZoneId, SetCommandType.Volume, target, Token);

			var after = await session.ExecuteAsync(new QueryCommand(AmpId, ZoneIdentifier.All), Token);

			foreach (var response in after.Response.Where(x => x.ZoneId != (int)ZoneId))
			{
				Assert.Equal(others[response.ZoneId], response.Response);
			}
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// A set command must be acknowledged without returning a status line.
	/// </summary>
	/// <remarks>
	/// Sets are silent on this protocol, so the port expects no payload. If it were to wait for one, the exchange
	/// would consume the next command's response and desynchronise the link, which is why the following query is part
	/// of the test.
	/// </remarks>
	[HardwareMutationFact]
	public async Task SetCommand_ReturnsNoResponseAndLeavesLinkInSync()
	{
		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			var result = await session.ExecuteAsync(
				SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1),
				Token);

			Assert.Empty(result.Response);

			var response = await session.QueryZoneAsync(AmpId, ZoneId, Token);
			Assert.Equal((int)ZoneId, response.ZoneId);
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// A burst of writes must all be applied, with the last one winning.
	/// </summary>
	/// <remarks>
	/// This is the volume knob case: a user dragging a slider produces a rapid stream of sets. The failure it guards
	/// against is the amplifier or the port dropping intermediate commands and settling on a stale value, which would
	/// leave the UI and the hardware disagreeing.
	/// </remarks>
	[HardwareMutationFact]
	public async Task RapidSetCommands_SettleOnTheLastValue()
	{
		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);

			var values = new[] { 10, 11, 12, 13, 14, 15 };

			foreach (var value in values)
			{
				await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Volume, value), Token);
			}

			await Task.Delay(Options.SettleDelay, Token);

			var response = await session.QueryZoneAsync(AmpId, ZoneId, Token);
			Assert.Equal(values[^1], response.Volume);
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// Muting a zone must not discard the volume it will return to.
	/// </summary>
	/// <remarks>
	/// Mute and volume are separate fields, and a client that reads volume while muted needs the underlying value
	/// rather than zero, otherwise unmuting through the UI restores silence.
	/// </remarks>
	[HardwareMutationFact]
	public async Task Mute_PreservesTheReportedVolume()
	{
		await using var session = await OpenSessionAsync();

		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, AmpId, ZoneId, Token);

		try
		{
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Power, 1), Token);
			await session.ExecuteAsync(SetCommand.Create(AmpId, ZoneId, SetCommandType.Mute, 0), Token);

			var unmuted = await session.SetAndReadBackAsync(AmpId, ZoneId, SetCommandType.Volume, 12, Token);
			Assert.Equal(12, unmuted.Volume);

			var muted = await session.SetAndReadBackAsync(AmpId, ZoneId, SetCommandType.Mute, 1, Token);

			Assert.True(muted.Mute);
			Assert.Equal(12, muted.Volume);
		}
		finally
		{
			await snapshot.RestoreAsync(session, CleanupToken);
		}
	}

	public static TheoryData<string> SettableProperties()
	{
		var data = new TheoryData<string>();

		// Driven from the command table rather than a hand written list, so a property added there is exercised here
		// without anyone having to remember to add it.
		foreach (var commandType in SetCommandType.Settable)
		{
			data.Add(commandType.CommandString);
		}

		return data;
	}

	/// <summary>
	/// Resolves a command type from the name carried in the theory data.
	/// </summary>
	/// <remarks>
	/// <see cref="SetCommandType"/> is not part of the public surface in a form xUnit can serialise, and unserialisable
	/// theory data is reported as a single opaque case, so the name is passed instead and exchanged for the singleton
	/// here.
	/// </remarks>
	private static SetCommandType Resolve(string name)
		=> SetCommandType.Settable.Single(x => x.CommandString == name);

	/// <summary>
	/// Picks a value inside the property's range that differs from its current one.
	/// </summary>
	/// <remarks>
	/// Prefers the midpoint, which is a valid value for every property and away from the boundaries, and steps off it
	/// only when the zone already reports it. A target equal to the current value would let a write that did nothing
	/// pass.
	/// </remarks>
	private static int ChooseTarget(SetCommandType commandType, int current)
	{
		var midpoint = commandType.MinValue + ((commandType.MaxValue - commandType.MinValue) / 2);

		if (midpoint != current)
		{
			return midpoint;
		}

		return midpoint == commandType.MaxValue
			? commandType.MinValue
			: midpoint + 1;
	}

	private static int ValueOf(CommandResponse response, SetCommandType commandType)
	{
		if (commandType == SetCommandType.Power) return response.Power ? 1 : 0;
		if (commandType == SetCommandType.Mute) return response.Mute ? 1 : 0;
		if (commandType == SetCommandType.DoNotDisturb) return response.DoNotDisturb ? 1 : 0;
		if (commandType == SetCommandType.Volume) return response.Volume;
		if (commandType == SetCommandType.Treble) return response.Treble;
		if (commandType == SetCommandType.Bass) return response.Bass;
		if (commandType == SetCommandType.Balance) return response.Balance;
		if (commandType == SetCommandType.Source) return response.Source;

		throw new ArgumentOutOfRangeException(nameof(commandType), commandType.CommandString, "Not a settable property.");
	}
}
