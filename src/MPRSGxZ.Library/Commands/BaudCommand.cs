using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Commands;

internal record BaudCommand(Baud Value) : ICommand
{
	public int ExpectedLines => 0;

	public override string ToString() => $"<{(int)Value}";
	public static implicit operator string(BaudCommand command) => command.ToString();
}