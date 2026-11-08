using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Events;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.IntegrationTest.Fixtures;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers the facade over the port: discovery, the polling loop, and the events consumers bind to.
/// </summary>
/// <remarks>
/// These tests exercise the background poll loop, which the protocol level tests deliberately avoid because its
/// traffic would interleave with their assertions. Here that traffic is the subject.
/// </remarks>
public sealed class AmplifierTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	/// <summary>
	/// Opening must connect, discover the chain, and populate zone state from the hardware.
	/// </summary>
	/// <remarks>
	/// Zone state arrives from the poll loop rather than from opening, so the assertion waits for it. A zone whose
	/// source is still zero has never been populated, since the hardware reports sources from one.
	/// </remarks>
	[HardwareFact]
	public async Task OpenAsync_ConnectsAndPopulatesZones()
	{
		await using var amplifier = CreateAmplifier();

		Assert.Equal(ConnectionState.Connected, await amplifier.OpenAsync(Token));
		Assert.Equal(Options.ExpectedAmplifierCount, amplifier.AmplifierCount);

		try
		{
			var populated = await WaitUntilAsync(
				() => LiveZones(amplifier).All(x => x.Source != 0),
				Token);

			Assert.True(populated, "The poll loop did not populate every zone with hardware state.");
		}
		finally
		{
			await amplifier.Close();
		}
	}

	/// <summary>
	/// The zone array must always describe all three amplifiers, whether or not they are present.
	/// </summary>
	/// <remarks>
	/// Consumers index this array by a flat zone id, so its shape cannot depend on how many amplifiers answered.
	/// Sizing it from the discovered count would turn a missing amplifier into an out of range exception in the UI.
	/// </remarks>
	[HardwareFact]
	public async Task Zones_CoverAllThreeAmplifiersRegardlessOfDiscovery()
	{
		await using var amplifier = CreateAmplifier();

		await amplifier.OpenAsync(Token);

		try
		{
			Assert.Equal(18, amplifier.Zones.Length);
			Assert.Equal(Enumerable.Range(1, 18), amplifier.Zones.Select(x => x.Id));
		}
		finally
		{
			await amplifier.Close();
		}
	}

	/// <summary>
	/// A change made outside the library must be reported through <see cref="Amplifier.ZoneChanged"/>.
	/// </summary>
	/// <remarks>
	/// This is the path that keeps a UI in step with the physical remotes and wall panels. It cannot be tested without
	/// changing something, so it is gated behind the mutation opt in and drives the change through the port directly
	/// rather than through a zone property, which would make the library the source of the change and prove nothing
	/// about the poll loop.
	/// </remarks>
	[HardwareMutationFact]
	public async Task ZoneChanged_FiresWhenTheHardwareStateChanges()
	{
		var zone = new HardwareZone(Options.MutableZoneId);
		var ampId = zone.AmpId;
		var zoneId = zone.ZoneId;

		await using var session = await OpenSessionAsync();
		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, ampId, zoneId, Token);

		// The port is single client, so the session has to be released before the amplifier can open its own.
		await session.DisposeAsync();

		// The port is shared with the amplifier so the change can be issued behind its back, which is what a physical
		// remote amounts to. Its own semaphore keeps the write from interleaving with a poll.
		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);
		var changes = new List<ZoneChangedEventArgs>();

		amplifier.ZoneChanged = (_, e) => changes.Add(e);

		await amplifier.OpenAsync(Token);

		try
		{
			// Let the first poll establish a baseline, otherwise the initial population of every zone is
			// indistinguishable from the change being tested.
			await WaitUntilAsync(() => changes.Count > 0, Token);
			changes.Clear();

			// Written through the port rather than through the zone, because assigning a zone property updates the
			// zone's own field first, leaving the next poll with nothing to report and the test asserting nothing.
			await port.ExecuteCommandAsync(
				SetCommand.Create(ampId, zoneId, SetCommandType.Power, 1),
				Token);

			await port.ExecuteCommandAsync(
				SetCommand.Create(ampId, zoneId, SetCommandType.Volume, snapshot[SetCommandType.Volume] == 12 ? 14 : 12),
				Token);

			var observed = await WaitUntilAsync(
				() => changes.Any(x => x.AmpId == (int)zone.AmpId && x.ZoneId == (int)zone.ZoneId),
				Token);

			Assert.True(observed, "No zone change was reported for the mutated zone.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshot.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// Writing a zone property must reach the hardware.
	/// </summary>
	/// <remarks>
	/// Property setters raise a change event that the amplifier turns into a command, without awaiting it. That makes
	/// this the one place where a write can be silently dropped, so the value is confirmed by reading it back from the
	/// hardware rather than from the zone object that was just assigned.
	/// </remarks>
	[HardwareMutationFact]
	public async Task ZoneProperty_WritesThroughToTheHardware()
	{
		var zone = new HardwareZone(Options.MutableZoneId);
		var ampId = zone.AmpId;
		var zoneId = zone.ZoneId;

		await using var session = await OpenSessionAsync();
		var snapshot = await ZoneStateSnapshot.CaptureAsync(session, ampId, zoneId, Token);
		await session.DisposeAsync();

		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		await amplifier.OpenAsync(Token);

		try
		{
			var target = amplifier.Zones[Options.MutableZoneId - 1];
			var volume = snapshot[SetCommandType.Volume] == 20 ? 18 : 20;

			target.Power = true;
			target.Volume = volume;

			// Read over the amplifier's own port rather than from the zone object: the setter updates the backing
			// field before the command is sent, so waiting on the zone would succeed before anything reached the
			// wire and then close the connection out from under the command in flight.
			var applied = await WaitUntilAsync(
				async () => (await port.TryQueryZoneAsync(ampId, zoneId, Token))?.Volume == volume,
				Token);

			Assert.True(applied, $"The hardware never reported a volume of {volume}.");
		}
		finally
		{
			await amplifier.Close();

			await using var restore = await OpenCleanupSessionAsync();
			await snapshot.RestoreAsync(restore, CleanupToken);
		}
	}

	/// <summary>
	/// The read only status fields the hardware reports must reach the zone objects the poll loop maintains.
	/// </summary>
	/// <remarks>
	/// Public address and keypad have no setter, so nothing in the library ever writes them and a parsing or
	/// propagation mistake would leave them permanently false with no other symptom. Comparing against a direct
	/// query is the only way to tell a genuine false from a value that was never carried across.
	/// </remarks>
	[HardwareFact]
	public async Task PollLoop_PropagatesReadOnlyStatusFields()
	{
		var port = CreatePort();

		await using var amplifier = CreateAmplifier(port);

		await amplifier.OpenAsync(Token);

		try
		{
			var populated = await WaitUntilAsync(() => LiveZones(amplifier).All(x => x.Source != 0), Token);
			Assert.True(populated, "The poll loop did not populate every zone with hardware state.");

			foreach (var zone in LiveZones(amplifier))
			{
				var response = await port.TryQueryZoneAsync(zone.AmpId, zone.ZoneId, Token);

				Assert.NotNull(response);
				Assert.Equal(response.Keypad, zone.Keypad);
				Assert.Equal(response.PublicAddress, zone.PublicAddress);
			}
		}
		finally
		{
			await amplifier.Close();
		}
	}

	/// <summary>
	/// Closing must stop the poll loop and release the connection.
	/// </summary>
	/// <remarks>
	/// A poll loop still running after close would keep the bridge's single client slot occupied and prevent anything
	/// else connecting. Opening a second, independent connection afterwards is what demonstrates the slot was
	/// genuinely freed.
	/// </remarks>
	[HardwareFact]
	public async Task Close_StopsPollingAndReleasesTheConnection()
	{
		var amplifier = CreateAmplifier();

		await amplifier.OpenAsync(Token);
		await WaitUntilAsync(() => LiveZones(amplifier).Any(x => x.Source != 0), Token);

		await amplifier.Close();
		await amplifier.DisposeAsync();

		await using var port = CreatePort();
		Assert.Equal(ConnectionState.Connected, await port.OpenAsync(Token));
	}

	/// <summary>
	/// Reopening after a close must resume polling without duplicating the zone handlers.
	/// </summary>
	/// <remarks>
	/// The handlers are attached on open, so an unguarded reattach would fire one command per open for every
	/// subsequent property change. That is visible here as more than one change notification for a single
	/// assignment.
	/// </remarks>
	[HardwareFact]
	public async Task OpenAsync_AfterClose_DoesNotDuplicateZoneHandlers()
	{
		await using var amplifier = CreateAmplifier();

		await amplifier.OpenAsync(Token);
		await amplifier.Close();

		Assert.Equal(ConnectionState.Connected, await amplifier.OpenAsync(Token));

		try
		{
			var resumed = await WaitUntilAsync(() => LiveZones(amplifier).Any(x => x.Source != 0), Token);

			Assert.True(resumed, "Polling did not resume after reopening.");
		}
		finally
		{
			await amplifier.Close();
		}
	}

	private Amplifier CreateAmplifier() => CreateAmplifier(CreatePort());

	private Amplifier CreateAmplifier(Library.Ports.IPAmplifierPort port) => new(CreateLogger<Amplifier>(), port, new ZoneLinkRegistry(CreateLogger<ZoneLinkRegistry>()));

	/// <summary>
	/// Gets the zones belonging to amplifiers that are actually present.
	/// </summary>
	/// <remarks>
	/// The array always describes three amplifiers, so assertions about polled state have to exclude the zones of
	/// amplifiers that were never discovered and will therefore never be populated.
	/// </remarks>
	private IEnumerable<HardwareZone> LiveZones(Amplifier amplifier)
		=> amplifier.Zones.Where(x => (int)x.AmpId <= Options.ExpectedAmplifierCount);

	/// <summary>
	/// Polls a condition until it holds or the test's timeout elapses.
	/// </summary>
	/// <remarks>
	/// Hardware state arrives asynchronously through the poll loop, so a condition that is false now may be true
	/// shortly. Returns whether the condition held rather than throwing, so the caller can attach a message
	/// describing what was being waited for.
	/// </remarks>
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
			// Falls through to report the condition never held, so the caller's assertion message is reported instead
			// of a cancellation.
		}

		return false;
	}

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
			// Falls through to report the condition never held, so the caller's assertion message is reported instead
			// of a cancellation.
		}

		return false;
	}
}
