using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Events;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.IntegrationTest.Fixtures;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers the secondary zone feature, where one zone mirrors another, against real hardware.
/// </summary>
/// <remarks>
/// Mirroring is a library concept: the amplifier has no notion of a linked zone, so every mirrored value reaches the
/// secondary as an ordinary set command that the library issues on the operator's behalf. That makes it worth testing
/// against hardware rather than in isolation, because the fan-out is only correct if each of those extra commands is
/// addressed to the right zone and is actually accepted.
///
/// Two independent paths keep a secondary in step and both are covered here. A write to the primary fans out
/// immediately, and the poll loop separately reconciles any drift it observes, which is what catches a change made
/// from a wall panel. A test that only covered the first would pass with reconciliation entirely broken.
///
/// All of these write to zones the operator did not individually nominate, so each brackets the zones it touches with
/// a multi zone snapshot and restores them on the way out.
/// </remarks>
public sealed class ZoneLinkTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	/// <summary>
	/// The zones used as the primary and its secondaries, in flat 1-18 numbering.
	/// </summary>
	/// <remarks>
	/// Fixed to the first amplifier, the one guaranteed to be present on any rig. Zone one leads because mirroring
	/// only fans out downstream of whichever zone is nominated as primary, and using a low numbered primary with
	/// higher numbered secondaries makes an off by one in either direction land on a zone that is under test.
	/// </remarks>
	private const int PrimaryZoneId = 1;
	private const int SecondaryZoneId = 2;
	private const int OtherSecondaryZoneId = 3;
	private const int UnlinkedZoneId = 4;

	private static IReadOnlyList<int> InvolvedZoneIds =>
		[PrimaryZoneId, SecondaryZoneId, OtherSecondaryZoneId, UnlinkedZoneId];

	/// <summary>
	/// Writing a mirrored property on a primary must apply the same value to its secondary on the hardware.
	/// </summary>
	/// <remarks>
	/// Driven through the zone property rather than the port, because it is the property setter's change notification
	/// that triggers the fan-out; writing through the port would bypass the feature entirely. The secondary is then
	/// confirmed by reading the hardware, so a fan-out that updated the in-memory zone but never reached the
	/// amplifier still fails.
	/// </remarks>
	[HardwareMutationFact]
	public async Task PrimaryWrite_MirrorsToTheSecondaryOnTheHardware()
	{
		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);
		await setup.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];

			primary.Power = true;

			var volume = primary.Volume == 22 ? 24 : 22;
			primary.Volume = volume;

			var (ampId, zoneId) = ZoneStateSnapshotSet.Address(SecondaryZoneId);

			var mirrored = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(ampId, zoneId, Token))?.Volume == volume,
				Token);

			Assert.True(mirrored, $"The hardware never reported the primary's volume of {volume} on the secondary.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// A primary must fan a write out to every one of its secondaries.
	/// </summary>
	/// <remarks>
	/// A primary may have any number of secondaries, and the fan-out loops over them, so a mistake that stopped after
	/// the first is invisible with only one link configured. Two secondaries is the smallest arrangement that
	/// distinguishes the two.
	/// </remarks>
	[HardwareMutationFact]
	public async Task PrimaryWrite_ReachesEverySecondary()
	{
		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);
		await setup.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);
		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, OtherSecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];

			primary.Power = true;

			var source = primary.Source == 3 ? 4 : 3;
			primary.Source = source;

			var first = ZoneStateSnapshotSet.Address(SecondaryZoneId);
			var second = ZoneStateSnapshotSet.Address(OtherSecondaryZoneId);

			var mirrored = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(first.AmpId, first.ZoneId, Token))?.Source == source
					&& (await port.TryQueryZoneAsync(second.AmpId, second.ZoneId, Token))?.Source == source,
				Token);

			Assert.True(mirrored, $"The hardware did not report source {source} on both secondaries.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// A write to a primary must leave zones that are not linked to it alone.
	/// </summary>
	/// <remarks>
	/// The counterpart to the fan-out tests. Mirroring writes to zones the operator never named, so its blast radius
	/// matters as much as its reach: a fan-out that walked every zone instead of the linked ones would satisfy every
	/// other test here.
	/// </remarks>
	[HardwareMutationFact]
	public async Task PrimaryWrite_LeavesUnlinkedZonesAlone()
	{
		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);

		// Give the unlinked zone a known value that differs from what the primary is about to be set to.
		var (unlinkedAmpId, unlinkedZoneId) = ZoneStateSnapshotSet.Address(UnlinkedZoneId);

		await setup.ExecuteAsync(SetCommand.Create(unlinkedAmpId, unlinkedZoneId, SetCommandType.Power, 1), Token);
		await setup.ExecuteAsync(SetCommand.Create(unlinkedAmpId, unlinkedZoneId, SetCommandType.Volume, 8), Token);

		await setup.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];

			primary.Power = true;
			primary.Volume = 26;

			var (secondaryAmpId, secondaryZoneId) = ZoneStateSnapshotSet.Address(SecondaryZoneId);

			var mirrored = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(secondaryAmpId, secondaryZoneId, Token))?.Volume == 26,
				Token);

			Assert.True(mirrored, "The secondary never took the primary's value, so the blast radius is untested.");

			var unlinked = await port.TryQueryZoneAsync(unlinkedAmpId, unlinkedZoneId, Token);

			Assert.Equal(8, unlinked?.Volume);
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// Unlinking must stop a zone tracking its former primary.
	/// </summary>
	/// <remarks>
	/// Links are configured at runtime, so removing one has to take effect as reliably as adding one. Mirroring first
	/// and then unlinking, rather than simply never linking, is what proves the removal did something.
	/// </remarks>
	[HardwareMutationFact]
	public async Task UnlinkZone_StopsMirroring()
	{
		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);
		await setup.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];
			var (ampId, zoneId) = ZoneStateSnapshotSet.Address(SecondaryZoneId);

			primary.Power = true;
			primary.Volume = 12;

			var mirrored = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(ampId, zoneId, Token))?.Volume == 12,
				Token);

			Assert.True(mirrored, "The secondary never took the primary's value, so unlinking cannot be tested.");

			amplifier.ZoneLinks.UnlinkZone(PrimaryZoneId, SecondaryZoneId);

			primary.Volume = 30;

			// Waited out rather than polled for: the assertion is that nothing happens, so the test has to give the
			// fan-out and a few poll rounds long enough to have happened before concluding they did not.
			var followed = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(ampId, zoneId, Token))?.Volume == 30,
				NegativeWait,
				Token);

			Assert.False(followed, "The secondary kept mirroring the primary after being unlinked.");

			var settled = await port.TryQueryZoneAsync(ampId, zoneId, Token);

			Assert.Equal(12, settled?.Volume);
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// The poll loop must pull a drifting secondary back onto its primary and report the correction.
	/// </summary>
	/// <remarks>
	/// This is the path that handles a secondary being changed from a wall panel or handset, where the library never
	/// sees a write at all and only the poll loop notices the divergence. The change is therefore issued through the
	/// port directly, behind the amplifier's back, which is what a physical control amounts to; driving it through
	/// the zone property would make the library the source of the change and exercise the fan-out instead.
	/// </remarks>
	[HardwareMutationFact]
	public async Task Polling_ReconcilesASecondaryThatDriftedFromItsPrimary()
	{
		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);
		await setup.DisposeAsync();

		// Shared with the amplifier so the divergence can be written without the library observing it. The port's own
		// semaphore keeps the write from interleaving with a poll.
		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);
		var changes = new List<ZoneChangedEventArgs>();

		amplifier.ZoneChanged = (_, e) => changes.Add(e);
		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];

			primary.Power = true;
			primary.Volume = 16;

			var (secondaryAmpId, secondaryZoneId) = ZoneStateSnapshotSet.Address(SecondaryZoneId);

			var settled = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(secondaryAmpId, secondaryZoneId, Token))?.Volume == 16,
				Token);

			Assert.True(settled, "The secondary never took the primary's value, so drift cannot be introduced.");

			changes.Clear();

			await port.ExecuteCommandAsync(
				SetCommand.Create(secondaryAmpId, secondaryZoneId, SetCommandType.Volume, 6),
				Token);

			// Asserted against the hardware rather than the zone object. The library corrects its own copy of the
			// secondary as part of reconciling, so the in-memory value never visibly leaves the primary's and would
			// satisfy this the instant it was checked, whether or not a correction was ever sent.
			var reconciled = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(secondaryAmpId, secondaryZoneId, Token))?.Volume == 16,
				Token);

			Assert.True(
				reconciled,
				"The hardware kept the drifted volume on the secondary instead of it being pulled back to 16.");

			Assert.Contains(
				changes,
				x => x.AmpId == (int)secondaryAmpId && x.ZoneId == (int)secondaryZoneId);
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// Every mirrored property must reach the secondary, not just the ones a single test happens to use.
	/// </summary>
	/// <remarks>
	/// The mirrored set is derived from the command registry, so a newly added property joins it automatically and
	/// would otherwise go untested. Running the same assertion over the whole set means a property that mirrors in
	/// principle but fails on the wire is caught by the suite rather than in the field.
	///
	/// Power is excluded because powering the primary off resets the secondary's other properties, which makes the
	/// read back ambiguous; it is covered implicitly by every other case here, all of which power the zone on first.
	/// </remarks>
	[HardwareMutationTheory]
	[MemberData(nameof(MirroredProperties))]
	public async Task EveryMirroredProperty_ReachesTheSecondary(string commandTypeName)
	{
		var commandType = SetCommandType.Mirrored.Single(x => x.CommandString == commandTypeName);

		await using var setup = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(setup, InvolvedZoneIds, Token);
		await setup.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		amplifier.ZoneLinks.LinkZone(PrimaryZoneId, SecondaryZoneId);

		await amplifier.OpenAsync(Token);

		try
		{
			var primary = amplifier.Zones[PrimaryZoneId - 1];
			var (ampId, zoneId) = ZoneStateSnapshotSet.Address(SecondaryZoneId);

			primary.Power = true;

			var powered = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(ampId, zoneId, Token))?.Power == true,
				Token);

			Assert.True(powered, "The secondary never powered on, so no other property can be mirrored to it.");

			// Derived from the hardware rather than from the primary's in-memory value, which may not have been
			// polled yet. A target the secondary already holds would satisfy the wait below without a command ever
			// reaching the wire, and the test would then assert nothing.
			var before = await port.TryQueryZoneAsync(ampId, zoneId, Token);

			Assert.NotNull(before);

			// It also has to differ from the primary's own value, or assigning it raises no change notification and
			// no fan-out is triggered in the first place.
			var target = ChooseTarget(
				commandType,
				PortProbe.ValueOf(before, commandType),
				Read(primary, commandType));

			Write(primary, commandType, target);

			var mirrored = await WaitUntilAsync(
				async () =>
				{
					var response = await port.TryQueryZoneAsync(ampId, zoneId, Token);

					return response is not null && PortProbe.ValueOf(response, commandType) == target;
				},
				Token);

			Assert.True(mirrored, $"The hardware never reported {commandType} of {target} on the secondary.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// Names the mirrored properties for the theory.
	/// </summary>
	/// <remarks>
	/// The name is passed rather than the command type because <see cref="SetCommandType"/> is not serialisable in a
	/// form xUnit can render, and unserialisable theory data collapses into a single opaque case.
	/// </remarks>
	public static TheoryData<string> MirroredProperties()
	{
		var data = new TheoryData<string>();

		foreach (var commandType in SetCommandType.Mirrored.Where(x => x != SetCommandType.Power))
		{
			data.Add(commandType.CommandString);
		}

		return data;
	}

	private static int Read(HardwareZone zone, SetCommandType commandType)
	{
		if (commandType == SetCommandType.Power) return zone.Power ? 1 : 0;
		if (commandType == SetCommandType.Mute) return zone.Mute ? 1 : 0;
		if (commandType == SetCommandType.DoNotDisturb) return zone.DoNotDisturb ? 1 : 0;
		if (commandType == SetCommandType.Volume) return zone.Volume;
		if (commandType == SetCommandType.Treble) return zone.Treble;
		if (commandType == SetCommandType.Bass) return zone.Bass;
		if (commandType == SetCommandType.Balance) return zone.Balance;
		if (commandType == SetCommandType.Source) return zone.Source;

		throw new ArgumentOutOfRangeException(nameof(commandType), commandType.CommandString, "Not a mirrored property.");
	}

	private static void Write(HardwareZone zone, SetCommandType commandType, int value)
	{
		if (commandType == SetCommandType.Power) zone.Power = value != 0;
		else if (commandType == SetCommandType.Mute) zone.Mute = value != 0;
		else if (commandType == SetCommandType.DoNotDisturb) zone.DoNotDisturb = value != 0;
		else if (commandType == SetCommandType.Volume) zone.Volume = value;
		else if (commandType == SetCommandType.Treble) zone.Treble = value;
		else if (commandType == SetCommandType.Bass) zone.Bass = value;
		else if (commandType == SetCommandType.Balance) zone.Balance = value;
		else if (commandType == SetCommandType.Source) zone.Source = value;
		else throw new ArgumentOutOfRangeException(nameof(commandType), commandType.CommandString, "Not a mirrored property.");
	}

	/// <summary>
	/// Picks a value inside the property's range that differs from every value the caller wants to move away from.
	/// </summary>
	/// <remarks>
	/// Mirroring tests have to avoid two separate values: the primary's, because assigning the value it already holds
	/// raises no change notification and so triggers no fan-out, and the secondary's, because a wait for a value it
	/// already holds succeeds without anything having reached the wire.
	/// </remarks>
	private static int ChooseTarget(SetCommandType commandType, params int[] avoid)
	{
		for (var value = commandType.MinValue; value <= commandType.MaxValue; value++)
		{
			if (!avoid.Contains(value))
			{
				return value;
			}
		}

		throw new ArgumentException(
			$"Every value of {commandType.CommandString} was excluded.",
			nameof(avoid));
	}

	private Amplifier CreateAmplifier() => CreateAmplifier(CreatePort());

	private Amplifier CreateAmplifier(Library.Ports.IPAmplifierPort port) => new(CreateLogger<Amplifier>(), port, new ZoneLinkRegistry(CreateLogger<ZoneLinkRegistry>()));

	/// <summary>
	/// How long to wait before concluding that something did not happen.
	/// </summary>
	/// <remarks>
	/// Comfortably longer than a fan-out plus several poll rounds. A negative assertion cannot use the test's full
	/// timeout, which would leave nothing for cleanup and turn every pass into a slow one.
	/// </remarks>
	private static readonly TimeSpan NegativeWait = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Polls a condition until it holds or the test's timeout elapses.
	/// </summary>
	private static Task<bool> WaitUntilAsync(Func<bool> condition, CancellationToken token)
		=> WaitUntilAsync(condition, Timeout.InfiniteTimeSpan, token);

	/// <summary>
	/// Polls a condition that has to query the hardware until it holds or the test's timeout elapses.
	/// </summary>
	private static Task<bool> WaitUntilAsync(Func<Task<bool>> condition, CancellationToken token)
		=> WaitUntilAsync(condition, Timeout.InfiniteTimeSpan, token);

	/// <summary>
	/// Polls a condition that has to query the hardware until it holds, the budget elapses, or the test times out.
	/// </summary>
	private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan budget, CancellationToken token)
	{
		using var timeout = new CancellationTokenSource(budget);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token);

		try
		{
			while (!linked.Token.IsCancellationRequested)
			{
				if (await condition())
				{
					return true;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
			}
		}
		catch (OperationCanceledException)
		{
			// Reported as the condition never holding, so the caller's message survives instead of a cancellation.
		}

		return false;
	}

	/// <summary>
	/// Polls a condition until it holds, the supplied budget elapses, or the test's timeout elapses.
	/// </summary>
	/// <remarks>
	/// Returns whether the condition held rather than throwing, so the caller can attach a message describing what was
	/// being waited for.
	/// </remarks>
	private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan budget, CancellationToken token)
	{
		using var timeout = new CancellationTokenSource(budget);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token);

		try
		{
			while (!linked.Token.IsCancellationRequested)
			{
				if (condition())
				{
					return true;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100), linked.Token);
			}
		}
		catch (OperationCanceledException)
		{
			// Reported as the condition never holding, so the caller's message survives instead of a cancellation.
		}

		return false;
	}
}
