using MPRSGxZ.Library.Enumerators;
using System.Net;

namespace MPRSGxZ.Web.Services;

public sealed partial class AmplifierService
{
	[LoggerMessage(4001, LogLevel.Information, "Loaded connection settings: {endpoint}")]
	private partial void LogLoadedConnectionSettings(IPEndPoint endpoint);

	[LoggerMessage(4002, LogLevel.Information, "Loaded {count} zone mirroring link(s).")]
	private partial void LogLoadedZoneMirroringLinks(int count);

	[LoggerMessage(4003, LogLevel.Debug, "Received ZoneChanged event for {ampId}{zoneId}")]
	private partial void LogReceivedZoneChangedEvent(int ampId, int zoneId);

	[LoggerMessage(4004, LogLevel.Information, "Initializer completed with result {result}")]
	private partial void LogInitializerCompleted(ConnectionState result);

	[LoggerMessage(4005, LogLevel.Information, "Reconnect requested; closing the current connection")]
	private partial void LogReconnectRequested();

	[LoggerMessage(4006, LogLevel.Information, "Reconnect completed with connection state {state}")]
	private partial void LogReconnectCompleted(ConnectionState state);

	[LoggerMessage(4007, LogLevel.Error, "Reconnect failed; the amplifier connection is now disconnected")]
	private partial void LogReconnectFailed(Exception ex);

	[LoggerMessage(4008, LogLevel.Information, "Amplifier service stopping")]
	private partial void LogStopping();

	[LoggerMessage(4009, LogLevel.Information, "Disposing amplifier service")]
	private partial void LogDisposing();
}
