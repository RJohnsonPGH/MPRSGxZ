using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Commands;

public class CommandResult(CommandResultStatus status, IEnumerable<CommandResponse> response)
{
	public CommandResultStatus Status => status;
	public IEnumerable<CommandResponse> Response => response;
}
