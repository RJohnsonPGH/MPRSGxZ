using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library;

public sealed partial class Amplifier
{
	[LoggerMessage(2001, LogLevel.Information, "Connection result {state}")]
	private partial void LogConnectionResult(ConnectionState state);

	[LoggerMessage(2002, LogLevel.Information, "Detected {count} amplifiers")]
	private partial void LogAmplifiersDetected(int count);

	[LoggerMessage(2003, LogLevel.Warning, "Failed to execute command: {command}")]
	private partial void LogCommandExecutionFailed(string command);

	[LoggerMessage(2004, LogLevel.Warning, "Failed to execute command due to timeout: {command}")]
	private partial void LogCommandExecutionTimeout(string command);

	[LoggerMessage(2005, LogLevel.Trace, "Successfully executed command: {command}")]
	private partial void LogCommandExecutionSuccessful(string command);

	[LoggerMessage(2006, LogLevel.Warning, "Failed to execute amplifier query: {command}")]
	private partial void LogAmplifierQueryFailed(string command);

	[LoggerMessage(2007, LogLevel.Trace, "Successfully polled amplifier: {command}")]
	private partial void LogAmplifierPolled(string command);

	[LoggerMessage(2008, LogLevel.Trace, "Poll response: {response}")]
	private partial void LogPollResponse(string response);

	[LoggerMessage(2009, LogLevel.Error, "Unhandled fault while applying {commandType} to zone {zoneId}")]
	private partial void LogUnhandledZoneCommandFault(Exception ex, int zoneId, string commandType);

	[LoggerMessage(2010, LogLevel.Error, "Zone {zoneId} reported {commandType} with unsupported value type {valueType}")]
	private partial void LogUnsupportedPropertyValue(int zoneId, string commandType, string valueType);

	[LoggerMessage(2011, LogLevel.Error, "Refused to send {commandType} value {value} for zone {zoneId} because it is not a valid command")]
	private partial void LogInvalidCommand(Exception ex, int zoneId, string commandType, int value);

	[LoggerMessage(2012, LogLevel.Warning, "Communication error while executing command: {command}")]
	private partial void LogCommandExecutionError(Exception ex, string command);

	[LoggerMessage(2013, LogLevel.Warning, "Poll iteration failed")]
	private partial void LogPollIterationFailed(Exception ex);

	[LoggerMessage(2014, LogLevel.Warning, "Discarded response for amplifier {ampId} zone {zoneId} because it maps outside the known zones")]
	private partial void LogResponseZoneOutOfRange(int ampId, int zoneId);

	[LoggerMessage(2015, LogLevel.Warning, "Attempting to reconnect after {failures} consecutive poll failures")]
	private partial void LogAttemptingReconnect(int failures);

	[LoggerMessage(2016, LogLevel.Debug, "Failed to cleanly close the port before reconnecting")]
	private partial void LogReconnectCloseFailed(Exception ex);

	[LoggerMessage(2017, LogLevel.Error, "Reconnect attempt returned {state}")]
	private partial void LogReconnectFailed(ConnectionState state);

	[LoggerMessage(2018, LogLevel.Error, "Reconnect attempt failed")]
	private partial void LogReconnectError(Exception ex);

	[LoggerMessage(2019, LogLevel.Information, "Reconnected to the amplifier, {count} amplifiers detected")]
	private partial void LogReconnected(int count);
}
