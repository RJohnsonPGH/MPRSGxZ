using Microsoft.Extensions.Logging;

namespace MPRSGxZ.CLI.Commands;

public sealed partial class MonitorCommand
{
	[LoggerMessage(0, LogLevel.Critical, "Endpoint provider is not an CommandLineEndpointProvider.")]
	public partial void LogInvalidEndpointProvider();

	[LoggerMessage(1, LogLevel.Critical, "Could not resolve address {Address}.")]
	public partial void LogCouldNotResolveAddress(string address);
}
