namespace MPRSGxZ.Web.Models;

public class SoftwareAmplifier
{
	public int Id { get; }
	public string Name { get; set; } = default!;
	public bool Enabled { get; set; }
}

public class SoftwareSource : SoftwareAmplifier { }

public class SoftwareZone : SoftwareAmplifier
{
	/// <summary>
	/// The Id of the zone this zone mirrors, or null if this zone is not a secondary.
	/// A zone with secondaries mirroring it (i.e. a primary) has no MirrorsZoneId of its own.
	/// </summary>
	public int? MirrorsZoneId { get; set; }
}
