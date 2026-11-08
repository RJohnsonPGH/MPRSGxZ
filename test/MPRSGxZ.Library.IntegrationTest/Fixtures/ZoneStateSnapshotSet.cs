using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Hardware;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Captures several zones at once so a test that writes across an amplifier can put all of them back.
/// </summary>
/// <remarks>
/// Tests that touch a single zone can bracket it with a <see cref="ZoneStateSnapshot"/> directly, but the mirroring
/// and whole amplifier tests write to zones the operator did not individually nominate. Restoring those one at a time
/// from the test body is easy to get wrong on a failure path, so the whole set is captured and restored as a unit.
///
/// Like the single zone snapshot, restore is best effort and never throws, so a restore problem cannot replace the
/// assertion that actually failed.
/// </remarks>
internal sealed class ZoneStateSnapshotSet
{
	private readonly IReadOnlyList<(int ZoneId, ZoneStateSnapshot Snapshot)> _snapshots;

	private ZoneStateSnapshotSet(IReadOnlyList<(int ZoneId, ZoneStateSnapshot Snapshot)> snapshots)
	{
		_snapshots = snapshots;
	}

	/// <summary>
	/// Reads the current state of every supplied zone, expressed in flat 1-18 numbering.
	/// </summary>
	public static async Task<ZoneStateSnapshotSet> CaptureAsync(
		HardwareSession session,
		IEnumerable<int> flatZoneIds,
		CancellationToken token)
	{
		var snapshots = new List<(int, ZoneStateSnapshot)>();

		foreach (var flatZoneId in flatZoneIds)
		{
			var zone = new HardwareZone(flatZoneId);

			snapshots.Add((flatZoneId, await ZoneStateSnapshot.CaptureAsync(session, zone.AmpId, zone.ZoneId, token)));
		}

		return new ZoneStateSnapshotSet(snapshots);
	}

	/// <summary>
	/// Gets the captured state of one zone, by flat zone id.
	/// </summary>
	public ZoneStateSnapshot this[int flatZoneId]
		=> _snapshots.Single(x => x.ZoneId == flatZoneId).Snapshot;

	/// <summary>
	/// Writes every captured zone back to the state it was captured in.
	/// </summary>
	public async Task RestoreAsync(HardwareSession session, CancellationToken token)
	{
		foreach (var (_, snapshot) in _snapshots)
		{
			await snapshot.RestoreAsync(session, token);
		}
	}

	/// <summary>
	/// Asserts that every captured zone currently matches its snapshot.
	/// </summary>
	public async Task AssertMatchesAsync(HardwareSession session, CancellationToken token)
	{
		foreach (var (_, snapshot) in _snapshots)
		{
			await snapshot.AssertMatchesAsync(session, token);
		}
	}

	/// <summary>
	/// Gets the flat zone ids of every amplifier that is present on the rig, six per amplifier.
	/// </summary>
	public static IReadOnlyList<int> FlatZoneIds(int amplifierId)
		=> [.. Enumerable.Range(((amplifierId - 1) * ZonesPerAmplifier) + 1, ZonesPerAmplifier)];

	/// <summary>
	/// The number of zones each amplifier addresses, including the two a four zone model reports but does not have.
	/// </summary>
	/// <remarks>
	/// Mirrors the same constant in <see cref="Amplifier"/>. A four zone amplifier still answers for zones five and
	/// six, so tests address all six deliberately: that the phantom zones behave like any other is part of what the
	/// whole amplifier test is confirming.
	/// </remarks>
	public const int ZonesPerAmplifier = 6;

	/// <summary>
	/// Gets the protocol address of a flat zone id.
	/// </summary>
	public static (AmplifierIdentifier AmpId, ZoneIdentifier ZoneId) Address(int flatZoneId)
	{
		var zone = new HardwareZone(flatZoneId);

		return (zone.AmpId, zone.ZoneId);
	}
}
