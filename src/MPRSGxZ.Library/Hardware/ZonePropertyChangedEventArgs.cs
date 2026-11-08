using MPRSGxZ.Library.Commands;

namespace MPRSGxZ.Library.Hardware;

public class ZonePropertyChangedEventArgs(SetCommandType commandType, object newValue) : EventArgs
{
	public SetCommandType SetCommandType { get; } = commandType;
	public object NewValue { get; } = newValue;
}
