namespace MPRSGxZ.Library.Events;

public class ZoneChangedEventArgs(int ampId, int zoneId) : EventArgs
{
	public int AmpId => ampId;
	public int ZoneId => zoneId;
}