using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Commands;

/// <summary>
/// Represents a command to query the status of a specific zone or all zones on a given amplifier.
/// </summary>
/// <remarks>When querying all zones, the expected response contains six lines, even for amplifiers that support
/// fewer than six zones. When querying a specific zone, the response contains a single line.</remarks>
/// <param name="AmpId">The identifier of the amplifier to query.</param>
/// <param name="ZoneId">The identifier of the zone to query. Use <see cref="ZoneIdentifier.All"/> to query all zones on the amplifier.</param>
internal record QueryCommand(AmplifierIdentifier AmpId, ZoneIdentifier ZoneId) : ICommand
{
	// If querying all zones, expect 6 lines (even 4 zone amps return zones 5 and 6), otherwise 1 line
	public int ExpectedLines => ZoneId == ZoneIdentifier.All ? 6 : 1;

	public override string ToString() => $"?{(int)AmpId}{(int)ZoneId}";
	public static implicit operator string(QueryCommand command) => command.ToString();
}