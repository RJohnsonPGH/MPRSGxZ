using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.IntegrationTest.Fixtures;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers the read side of the protocol: querying zones, framing of the responses, and amplifier discovery.
/// </summary>
/// <remarks>
/// These tests only read, so they run whenever hardware is enabled and need no mutation opt in.
/// </remarks>
public sealed class QueryProtocolTests(HardwareFixture fixture, ITestOutputHelper outputHelper)
	: HardwareTestBase(fixture, outputHelper)
{
	/// <summary>
	/// Discovery must find exactly the number of amplifiers on the chain.
	/// </summary>
	/// <remarks>
	/// Probing walks down from address three, and an absent amplifier answers with nothing but a re-printed prompt.
	/// Getting this wrong in the optimistic direction is the dangerous case: a phantom amplifier would be polled
	/// forever and every poll would fail.
	/// </remarks>
	[HardwareFact]
	public async Task GetAmplifierCountAsync_MatchesTheRig()
	{
		await using var session = await OpenSessionAsync();

		var count = await session.Port.GetAmplifierCountAsync(Token);

		Assert.Equal(Options.ExpectedAmplifierCount, count);
	}

	/// <summary>
	/// Discovery must be repeatable, which means each probe leaves the stream clean.
	/// </summary>
	/// <remarks>
	/// A failed probe leaves the amplifier's re-printed prompt in the buffer. If it is not drained, the next read
	/// starts mid-frame and the count comes back different, so running discovery repeatedly on one connection is the
	/// cheapest way to detect a missing drain.
	/// </remarks>
	[HardwareFact]
	public async Task GetAmplifierCountAsync_IsRepeatable()
	{
		await using var session = await OpenSessionAsync();

		var first = await session.Port.GetAmplifierCountAsync(Token);
		var second = await session.Port.GetAmplifierCountAsync(Token);
		var third = await session.Port.GetAmplifierCountAsync(Token);

		Assert.Equal(first, second);
		Assert.Equal(first, third);
	}

	/// <summary>
	/// A broadcast query must return one line per zone, including zones that a four zone amplifier does not have.
	/// </summary>
	[HardwareFact]
	public async Task QueryAllZones_ReturnsSixLines()
	{
		await using var session = await OpenSessionAsync();

		var result = await session.ExecuteAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All), Token);
		var responses = result.Response.ToList();

		Assert.Equal(6, responses.Count);

		// Zones are reported in ascending order, and the poll loop relies on the reported ids rather than position,
		// so both the ordering and the ids are worth pinning.
		Assert.Equal(Enumerable.Range(1, 6), responses.Select(x => x.ZoneId));
		Assert.All(responses, x => Assert.Equal(1, x.AmpId));
	}

	/// <summary>
	/// A single zone query must return exactly one line, for that zone.
	/// </summary>
	[HardwareTheory]
	[InlineData(ZoneIdentifier.One)]
	[InlineData(ZoneIdentifier.Two)]
	[InlineData(ZoneIdentifier.Three)]
	[InlineData(ZoneIdentifier.Four)]
	[InlineData(ZoneIdentifier.Five)]
	[InlineData(ZoneIdentifier.Six)]
	public async Task QuerySingleZone_ReturnsThatZone(ZoneIdentifier zoneId)
	{
		await using var session = await OpenSessionAsync();

		var response = await session.QueryZoneAsync(AmplifierIdentifier.One, zoneId, Token);

		Assert.Equal(1, response.AmpId);
		Assert.Equal((int)zoneId, response.ZoneId);
	}

	/// <summary>
	/// Every field of a live response must fall inside the range its command type declares.
	/// </summary>
	/// <remarks>
	/// This is the check that the field offsets in <see cref="CommandResponse.TryParse"/> line up with what the
	/// hardware actually sends. A misread offset usually still parses, because every character is a digit, and shows
	/// up only as an out of range value, for example a source of 38 where volume was read by mistake.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAllZones_FieldsAreWithinDeclaredRanges()
	{
		await using var session = await OpenSessionAsync();

		var result = await session.ExecuteAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All), Token);

		foreach (var response in result.Response)
		{
			Output.WriteLine($"Zone {response.ZoneId}: {response.Response}");

			Assert.InRange(response.Volume, SetCommandType.Volume.MinValue, SetCommandType.Volume.MaxValue);
			Assert.InRange(response.Treble, SetCommandType.Treble.MinValue, SetCommandType.Treble.MaxValue);
			Assert.InRange(response.Bass, SetCommandType.Bass.MinValue, SetCommandType.Bass.MaxValue);
			Assert.InRange(response.Balance, SetCommandType.Balance.MinValue, SetCommandType.Balance.MaxValue);
			Assert.InRange(response.Source, SetCommandType.Source.MinValue, SetCommandType.Source.MaxValue);
		}
	}

	/// <summary>
	/// Re-encoding a decoded response must reproduce the exact characters received.
	/// </summary>
	/// <remarks>
	/// Round tripping through <see cref="CommandResponse.ToString"/> proves that decoding is lossless and that the
	/// field widths used when writing match those used when reading. Any field that is decoded from the wrong offset,
	/// or written with the wrong width, shifts the re-encoded string.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAllZones_ResponsesRoundTripThroughToString()
	{
		await using var session = await OpenSessionAsync();

		var result = await session.ExecuteAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All), Token);

		foreach (var response in result.Response)
		{
			Assert.Equal(CommandResponse.ResponseLength, response.Response.Length);
			Assert.Equal(response.Response, response.ToString());
		}
	}

	/// <summary>
	/// Public address is an amplifier wide state, so every zone on an amplifier must report the same value.
	/// </summary>
	/// <remarks>
	/// This is the evidence for treating it as read-only rather than as a per zone setting. If the hardware ever
	/// reported it per zone, the decision to make it unsettable would need revisiting.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAllZones_PublicAddressIsUniformAcrossZones()
	{
		await using var session = await OpenSessionAsync();

		var result = await session.ExecuteAsync(new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All), Token);
		var distinct = result.Response.Select(x => x.PublicAddress).Distinct().ToList();

		Assert.Single(distinct);
	}

	/// <summary>
	/// Every amplifier the rig reports must answer a broadcast query for itself.
	/// </summary>
	/// <remarks>
	/// Discovery only establishes that the highest addressed amplifier answers. This confirms the contiguity
	/// assumption it relies on, namely that every lower address answers too.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAllZones_EveryDiscoveredAmplifierResponds()
	{
		await using var session = await OpenSessionAsync();

		var count = await session.Port.GetAmplifierCountAsync(Token);

		for (var ampId = 1; ampId <= count; ampId++)
		{
			var result = await session.ExecuteAsync(
				new QueryCommand((AmplifierIdentifier)ampId, ZoneIdentifier.All),
				Token);

			Assert.All(result.Response, x => Assert.Equal(ampId, x.AmpId));
		}
	}

	/// <summary>
	/// Consecutive queries must stay in step, with no residue carried between exchanges.
	/// </summary>
	/// <remarks>
	/// Desynchronisation is the characteristic protocol failure here: one exchange leaves bytes behind, the next read
	/// starts mid-frame and every subsequent command fails. A single query cannot detect it, so the exchange is
	/// repeated and each result checked, which is also what the poll loop does in normal operation.
	/// </remarks>
	[HardwareFact]
	public async Task RepeatedQueries_StayInSync()
	{
		await using var session = await OpenSessionAsync();

		for (var i = 0; i < 20; i++)
		{
			var result = await session.ExecuteAsync(
				new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
				Token);

			Assert.Equal(6, result.Response.Count());
		}
	}

	/// <summary>
	/// Interleaving broadcast and single zone queries must not desynchronise the link.
	/// </summary>
	/// <remarks>
	/// The two have different expected lengths, so alternating them exercises the case where a read sized for one
	/// response shape encounters the other. A port that sized its reads from a stale expectation would leave the
	/// difference in the buffer.
	/// </remarks>
	[HardwareFact]
	public async Task AlternatingQueryShapes_StayInSync()
	{
		await using var session = await OpenSessionAsync();

		for (var i = 0; i < 5; i++)
		{
			var all = await session.ExecuteAsync(
				new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
				Token);
			Assert.Equal(6, all.Response.Count());

			var single = await session.QueryZoneAsync(AmplifierIdentifier.One, ZoneIdentifier.One, Token);
			Assert.Equal(1, single.ZoneId);
		}
	}

	/// <summary>
	/// Commands issued concurrently must be serialised rather than interleaved on the wire.
	/// </summary>
	/// <remarks>
	/// The port holds a semaphore for the whole write and read exchange. Without it, two callers would interleave
	/// their bytes and both would read fragments of the other's response. Every task here queries a different zone and
	/// must receive that zone's line, which can only happen if the exchanges did not overlap.
	/// </remarks>
	[HardwareFact]
	public async Task ConcurrentCommands_AreSerialised()
	{
		await using var session = await OpenSessionAsync();

		var zones = new[]
		{
			ZoneIdentifier.One,
			ZoneIdentifier.Two,
			ZoneIdentifier.Three,
			ZoneIdentifier.Four,
			ZoneIdentifier.Five,
			ZoneIdentifier.Six
		};

		var responses = await Task.WhenAll(
			zones.Select(x => session.QueryZoneAsync(AmplifierIdentifier.One, x, Token)));

		Assert.Equal(
			zones.Select(x => (int)x),
			responses.Select(x => x.ZoneId));
	}

	/// <summary>
	/// Querying an amplifier that is not present must fail cleanly and leave the link usable.
	/// </summary>
	/// <remarks>
	/// An absent amplifier returns no payload at all, just the re-printed prompt, so this is the port's handling of an
	/// empty response. What matters is not only that the command reports failure, but that the following command
	/// against a real amplifier still succeeds: that is what stops one missing unit from breaking the poll loop
	/// permanently.
	/// </remarks>
	[HardwareFact]
	public async Task QueryAbsentAmplifier_FailsWithoutBreakingTheLink()
	{
		if (Options.ExpectedAmplifierCount >= 3)
		{
			Assert.Skip("Every amplifier address is populated on this rig, so there is no absent amplifier to query.");
		}

		await using var session = await OpenSessionAsync();

		var absent = (AmplifierIdentifier)(Options.ExpectedAmplifierCount + 1);

		var result = await session.Port.ExecuteCommandAsync(new QueryCommand(absent, ZoneIdentifier.All), Token);
		Assert.Equal(CommandResultStatus.Failed, result.Status);

		var recovered = await session.ExecuteAsync(
			new QueryCommand(AmplifierIdentifier.One, ZoneIdentifier.All),
			Token);
		Assert.Equal(6, recovered.Response.Count());
	}
}
