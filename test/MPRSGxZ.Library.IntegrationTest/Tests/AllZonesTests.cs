using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.IntegrationTest.Fixtures;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers addressing every zone of an amplifier rather than the single zone the other write tests share.
/// </summary>
/// <remarks>
/// The rest of the mutating suite works against one operator chosen zone, which is deliberate but leaves the zone
/// addressing itself barely exercised: a transposed digit or an off by one in the flat to per amplifier translation
/// would still pass every one of those tests. These write to all six zones, so an addressing mistake shows up as the
/// wrong zone changing.
///
/// Six zones are addressed even on a four zone amplifier. The hardware reports the two it does not have, and code
/// that indexes zones has to cope with that, so their behaviour is part of the subject rather than an accident.
/// </remarks>
public sealed class AllZonesTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	/// <summary>
	/// The amplifier whose zones are written to.
	/// </summary>
	/// <remarks>
	/// Fixed to the first amplifier rather than derived from the mutable zone. It is the one amplifier guaranteed to
	/// be present on any rig, and these tests write to all of its zones regardless of which one the operator
	/// nominated for single zone work.
	/// </remarks>
	private const int TargetAmplifierId = 1;

	private static IReadOnlyList<int> TargetZoneIds => ZoneStateSnapshotSet.FlatZoneIds(TargetAmplifierId);

	/// <summary>
	/// Every zone must be individually addressable, with a write reaching only the zone it named.
	/// </summary>
	/// <remarks>
	/// Each zone is given a distinct volume in one pass and the whole amplifier is then read back in a single query,
	/// so the assertion sees all six at once. Distinct values are what make this meaningful: if every zone were set
	/// to the same volume, a command that leaked to its neighbours would be indistinguishable from one that did not.
	/// </remarks>
	[HardwareMutationFact]
	public async Task EveryZone_IsIndividuallyAddressable()
	{
		await using var session = await OpenSessionAsync();

		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(session, TargetZoneIds, Token);

		try
		{
			// A distinct volume per zone, all comfortably inside the range and away from its ends.
			var expected = TargetZoneIds
				.Select((flatZoneId, index) => (FlatZoneId: flatZoneId, Volume: 10 + index))
				.ToList();

			foreach (var (flatZoneId, volume) in expected)
			{
				var (ampId, zoneId) = ZoneStateSnapshotSet.Address(flatZoneId);

				await session.ExecuteAsync(SetCommand.Create(ampId, zoneId, SetCommandType.Power, 1), Token);
				await session.ExecuteAsync(SetCommand.Create(ampId, zoneId, SetCommandType.Volume, volume), Token);
			}

			await Task.Delay(Options.SettleDelay, Token);

			var result = await session.ExecuteAsync(
				new QueryCommand((AmplifierIdentifier)TargetAmplifierId, ZoneIdentifier.All),
				Token);

			var byZone = result.Response.ToDictionary(x => x.ZoneId);

			foreach (var (flatZoneId, volume) in expected)
			{
				var (_, zoneId) = ZoneStateSnapshotSet.Address(flatZoneId);

				Assert.Equal(volume, byZone[(int)zoneId].Volume);
			}
		}
		finally
		{
			await snapshots.RestoreAsync(session, CleanupToken);
		}
	}

	/// <summary>
	/// A query for all zones must report each zone once, in ascending order.
	/// </summary>
	/// <remarks>
	/// Consumers index the parsed responses positionally, so a short or reordered reply would silently write one
	/// zone's state onto another. Asserted separately from the addressing test because it holds regardless of what
	/// the zones are set to.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAllZones_ReportsEveryZoneExactlyOnceInOrder()
	{
		await using var session = await OpenSessionAsync();

		var result = await session.ExecuteAsync(
			new QueryCommand((AmplifierIdentifier)TargetAmplifierId, ZoneIdentifier.All),
			Token);

		var responses = result.Response.ToList();

		Assert.Equal(ZoneStateSnapshotSet.ZonesPerAmplifier, responses.Count);
		Assert.Equal(Enumerable.Range(1, ZoneStateSnapshotSet.ZonesPerAmplifier), responses.Select(x => x.ZoneId));
		Assert.All(responses, x => Assert.Equal(TargetAmplifierId, x.AmpId));
	}

	/// <summary>
	/// The library's flat zone numbering must line up with the addresses the hardware answers on.
	/// </summary>
	/// <remarks>
	/// This is the translation that <see cref="HardwareZone"/> performs and that the poll loop reverses when it maps a
	/// response back onto the zone array. Verified against the hardware rather than in isolation, because a
	/// consistent mistake on both sides of that translation cancels out in a unit test and only shows up when a real
	/// amplifier is asked which zone it thinks it is.
	/// </remarks>
	[HardwareFact]
	public async Task FlatZoneNumbering_MatchesTheHardwareAddressing()
	{
		await using var session = await OpenSessionAsync();

		foreach (var flatZoneId in TargetZoneIds)
		{
			var (ampId, zoneId) = ZoneStateSnapshotSet.Address(flatZoneId);

			var response = await session.QueryZoneAsync(ampId, zoneId, Token);

			Assert.Equal((int)ampId, response.AmpId);
			Assert.Equal((int)zoneId, response.ZoneId);
		}
	}

	/// <summary>
	/// Writing every zone through the facade must reach all six, and the poll loop must report each one back.
	/// </summary>
	/// <remarks>
	/// The zone array is flat across all three amplifiers while the protocol addresses zones per amplifier, and the
	/// poll loop has to fold the amplifier address back in to find the entry a response belongs to. Driving all six
	/// zones through the facade at once is what would expose a mistake in either direction: the wrong zone changing,
	/// or every response landing on amplifier one's first zone.
	///
	/// Confirmed against the hardware over the amplifier's own port, not by reading the zone objects back. A zone
	/// property setter updates its backing field before the command is sent, so reading the zones would assert only
	/// that the assignments above happened.
	/// </remarks>
	[HardwareMutationFact]
	public async Task Facade_WritesAndPollsEveryZoneOfAnAmplifier()
	{
		await using var session = await OpenSessionAsync();
		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(session, TargetZoneIds, Token);

		// The port is single client, so the capture session has to be released before the facade can open its own.
		await session.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		await amplifier.OpenAsync(Token);

		try
		{
			var expected = TargetZoneIds
				.Select((flatZoneId, index) => (FlatZoneId: flatZoneId, Volume: 20 + index))
				.ToList();

			foreach (var (flatZoneId, volume) in expected)
			{
				var target = amplifier.Zones[flatZoneId - 1];

				target.Power = true;
				target.Volume = volume;
			}

			var applied = await WaitUntilAsync(
				async () =>
				{
					var zones = await port.TryQueryAmplifierAsync((AmplifierIdentifier)TargetAmplifierId, Token);

					return zones is not null
						&& expected.All(x => zones[(int)ZoneStateSnapshotSet.Address(x.FlatZoneId).ZoneId].Volume == x.Volume);
				},
				Token);

			Assert.True(applied, "Not every zone reported the volume written to it through the facade.");

			// The poll loop is the only thing that populates the zone objects, so this additionally covers the
			// mapping from a response back onto the right entry of the flat zone array.
			var polled = await WaitUntilAsync(
				() => expected.All(x => amplifier.Zones[x.FlatZoneId - 1].Volume == x.Volume),
				Token);

			var observed = string.Join(
				", ",
				expected.Select(x => $"zone {x.FlatZoneId} = {amplifier.Zones[x.FlatZoneId - 1].Volume} (wanted {x.Volume})"));

			Assert.True(polled, $"Polling did not report every zone's volume: {observed}.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshots.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// Restoring a whole amplifier must return every zone to the state it was captured in.
	/// </summary>
	/// <remarks>
	/// The other tests in this class rely on the multi zone restore to leave the rig as they found it, across more
	/// zones than any single zone test touches, so it is verified rather than trusted.
	/// </remarks>
	[HardwareMutationFact]
	public async Task ZoneStateSnapshotSet_RestoresEveryZone()
	{
		await using var session = await OpenSessionAsync();

		var snapshots = await ZoneStateSnapshotSet.CaptureAsync(session, TargetZoneIds, Token);

		try
		{
			foreach (var flatZoneId in TargetZoneIds)
			{
				var (ampId, zoneId) = ZoneStateSnapshotSet.Address(flatZoneId);

				await session.ExecuteAsync(SetCommand.Create(ampId, zoneId, SetCommandType.Power, 1), Token);
				await session.ExecuteAsync(SetCommand.Create(ampId, zoneId, SetCommandType.Volume, 17), Token);
				await session.ExecuteAsync(SetCommand.Create(ampId, zoneId, SetCommandType.Bass, 9), Token);
			}

			await Task.Delay(Options.SettleDelay, Token);
		}
		finally
		{
			await snapshots.RestoreAsync(session, CleanupToken);
		}

		await Task.Delay(Options.SettleDelay, Token);
		await snapshots.AssertMatchesAsync(session, Token);
	}

	private Amplifier CreateAmplifier(Library.Ports.IPAmplifierPort port) => new(CreateLogger<Amplifier>(), port, new ZoneLinkRegistry(CreateLogger<ZoneLinkRegistry>()));

	/// <summary>
	/// Polls a condition that has to query the hardware until it holds or the test's timeout elapses.
	/// </summary>
	private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, CancellationToken token)
	{
		try
		{
			while (!token.IsCancellationRequested)
			{
				if (await condition())
				{
					return true;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100), token);
			}
		}
		catch (OperationCanceledException)
		{
			// Reported as the condition never holding, so the caller's message survives instead of a cancellation.
		}

		return false;
	}

	/// <summary>
	/// Polls a condition until it holds or the test's timeout elapses.
	/// </summary>
	private static async Task<bool> WaitUntilAsync(Func<bool> condition, CancellationToken token)
	{
		try
		{
			while (!token.IsCancellationRequested)
			{
				if (condition())
				{
					return true;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(100), token);
			}
		}
		catch (OperationCanceledException)
		{
			// Reported as the condition never holding, so the caller's message survives instead of a cancellation.
		}

		return false;
	}
}
