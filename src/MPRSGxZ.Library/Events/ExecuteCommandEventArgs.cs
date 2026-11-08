using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Events;

public class ExecuteCommandEventArgs(AmplifierIdentifier ampId, ZoneIdentifier zoneId, SetCommandType commandType, int value) : EventArgs
{
	public AmplifierIdentifier AmpId => ampId;
	public ZoneIdentifier ZoneId => zoneId;
	public SetCommandType CommandType => commandType;
	public int Value => value;
}