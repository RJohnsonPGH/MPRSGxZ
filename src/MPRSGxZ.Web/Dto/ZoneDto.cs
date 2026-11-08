using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Web.Dto;

public class ZoneDto
{
	public int Id { get; set; }
	public string Name { get; set; } = default!;
	public bool Enabled { get; set; }

	/// <summary>
	/// The Id of the zone this zone mirrors, or null if this zone is not a secondary.
	/// </summary>
	public int? MirrorsZoneId { get; set; }

	/// <summary>
	/// Whether this zone is Unlinked, a Primary (has secondaries mirroring it), or a Secondary
	/// (mirrors another zone).
	/// </summary>
	public ZoneLinkMode LinkMode { get; set; }

	public int AmpId => ((Id - ZoneId) / 6) + 1;
	public int ZoneId => (Id % 6) + 1;

	public bool Power {  get; set; }
	public bool Mute { get; set; }
	public bool PublicAddress { get; set; }

	/// <summary>
	/// Whether a wall keypad is physically connected to this zone. Reported by the hardware and not settable.
	/// </summary>
	public bool Keypad { get; set; }

	public bool DoNotDisturb { get; set; }
	public int Volume { get; set; }
	public int Treble { get; set; }
	public int Bass { get; set; }
	public int Balance { get; set; }
	public int Source {  get; set; }
}
