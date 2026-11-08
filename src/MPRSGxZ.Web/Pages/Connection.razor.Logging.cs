using Microsoft.AspNetCore.Components;

namespace MPRSGxZ.Web.Pages;

// The LoggerMessage generator requires an ILogger *field*, but Blazor can only inject into properties. The log
// methods are therefore declared static with an explicit ILogger parameter and given the injected logger at the
// call site.
public partial class Connection
{
	[Inject]
	private ILogger<Connection> Logger { get; set; } = default!;

	[LoggerMessage(7001, LogLevel.Warning, "Rejected amplifier address '{address}': {reason}")]
	private static partial void LogAddressRejected(ILogger logger, string address, string reason);

	[LoggerMessage(7002, LogLevel.Information, "Amplifier address changed to '{address}'; reconnecting")]
	private static partial void LogAddressChanged(ILogger logger, string address);

	[LoggerMessage(7003, LogLevel.Error, "Failed to apply amplifier address '{address}'")]
	private static partial void LogAddressChangeFailed(ILogger logger, Exception ex, string address);

	[LoggerMessage(7004, LogLevel.Warning, "Rejected amplifier port {port}: {reason}")]
	private static partial void LogPortRejected(ILogger logger, int port, string reason);

	[LoggerMessage(7005, LogLevel.Information, "Amplifier port changed to {port}; reconnecting")]
	private static partial void LogPortChanged(ILogger logger, int port);

	[LoggerMessage(7006, LogLevel.Error, "Failed to apply amplifier port {port}")]
	private static partial void LogPortChangeFailed(ILogger logger, Exception ex, int port);

	[LoggerMessage(7007, LogLevel.Error, "Manual reconnect from the Connection page failed")]
	private static partial void LogReconnectButtonFailed(ILogger logger, Exception ex);
}
