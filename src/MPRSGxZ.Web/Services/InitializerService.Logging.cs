using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Web.Services;

public partial class InitializerService
{
	[LoggerMessage(6001, LogLevel.Information, "Amplifier initializer starting")]
	private partial void LogStarting();

	[LoggerMessage(6002, LogLevel.Information, "Amplifier initializer started with connection state {state}")]
	private partial void LogStarted(ConnectionState state);

	[LoggerMessage(6003, LogLevel.Error, "Amplifier initializer failed to open the amplifier connection; the application will start disconnected")]
	private partial void LogStartFailed(Exception ex);

	[LoggerMessage(6004, LogLevel.Information, "Amplifier initializer stopping")]
	private partial void LogStopping();

	[LoggerMessage(6005, LogLevel.Information, "Amplifier initializer stopped")]
	private partial void LogStopped();

	[LoggerMessage(6006, LogLevel.Error, "Amplifier initializer failed to release amplifier resources during shutdown")]
	private partial void LogStopFailed(Exception ex);
}
