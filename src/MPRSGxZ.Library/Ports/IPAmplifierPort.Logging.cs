using Microsoft.Extensions.Logging;
using System.Net;

namespace MPRSGxZ.Library.Ports;

public partial class IPAmplifierPort
{
	// Static because it is invoked from the _writeRetryPolicy field initializer, which runs before
	// "this" is available and therefore cannot call an instance-based LoggerMessage method.
	[LoggerMessage(1020, LogLevel.Information, "Retrying command transmission (attempt {attemptNumber})")]
	private static partial void LogCommandRetryAttempt(ILogger logger, int attemptNumber);

	[LoggerMessage(1001, LogLevel.Information, "Attempting to connect to endpoint {endpoint}")]
	private partial void LogConnectingToEndpoint(IPEndPoint endpoint);

	[LoggerMessage(1002, LogLevel.Error, "Failed to connect to endpoint {endpoint}")]
	private partial void LogConnectFailed(Exception ex, IPEndPoint endpoint);

	[LoggerMessage(1003, LogLevel.Information, "Attempting connection at maximum baud rate {baud}")]
	private partial void LogAttemptingMaxBaudRate(int baud);

	[LoggerMessage(1004, LogLevel.Information, "Attempting connection at minimum baud rate {baud}")]
	private partial void LogAttemptingMinBaudRate(int baud);

	[LoggerMessage(1005, LogLevel.Information, "Amplifier communication established at baud rate {baud}")]
	private partial void LogCommunicationEstablished(int baud);

	[LoggerMessage(1006, LogLevel.Information, "Attempting to upgrade baud rate from {minBaud} to {maxBaud}")]
	private partial void LogAttemptingBaudRateUpgrade(int minBaud, int maxBaud);

	[LoggerMessage(1007, LogLevel.Information, "Successfully upgraded to baud rate {baud}")]
	private partial void LogBaudRateUpgradeSuccessful(int baud);

	[LoggerMessage(1008, LogLevel.Warning, "Failed to upgrade to baud rate {baud}, error occurred")]
	private partial void LogBaudRateUpgradeFailed(Exception ex, int baud);

	[LoggerMessage(1009, LogLevel.Information, "Falling back to minimum baud rate {baud}")]
	private partial void LogFallingBackToMinBaudRate(int baud);

	[LoggerMessage(1010, LogLevel.Trace, "Transmitting option bytes {bytes}")]
	private partial void LogTransmittingOptionBytes(string bytes);

	[LoggerMessage(1011, LogLevel.Error, "Failed to establish communication with amplifier at any supported baud rate")]
	private partial void LogNoBaudRateNegotiated();

	[LoggerMessage(1012, LogLevel.Trace, "Transmitting command {command}")]
	private partial void LogTransmittingCommand(string command);

	[LoggerMessage(1013, LogLevel.Error, "Failed to transmit command {command}")]
	private partial void LogCommandTransmissionFailed(string command);

	[LoggerMessage(1014, LogLevel.Information, "Determining amplifier count. Checking for presence of amplifier {amplifier}")]
	private partial void LogDeterminingAmplifierCount(int amplifier);

	[LoggerMessage(1015, LogLevel.Information, "Amplifier count determined to be {count}")]
	private partial void LogAmplifierCountDetermined(int count);

	[LoggerMessage(1016, LogLevel.Error, "Failed to determine the amplifier count. No suitable response was detected")]
	private partial void LogFailedToDetermineAmplifierCount();

	[LoggerMessage(1017, LogLevel.Trace, "Transmitting bytes {bytes}")]
	private partial void LogTransmittingBytes(string bytes);

	[LoggerMessage(1018, LogLevel.Warning, "Echo did not match the transmitted data: {transmitted} - {echo}")]
	private partial void LogEchoMismatch(string transmitted, string echo);

	[LoggerMessage(1019, LogLevel.Information, "Clearing stream")]
	private partial void LogClearingStream();

	[LoggerMessage(1021, LogLevel.Warning, "Failed to establish communication at baud rate {baud}")]
	private partial void LogBaudRateValidationFailed(int baud);

	[LoggerMessage(1022, LogLevel.Debug, "Probe of amplifier {amplifier} failed")]
	private partial void LogProbeFailed(Exception ex, int amplifier);

	[LoggerMessage(1023, LogLevel.Warning, "Command {command} returned {count} malformed response lines: {raw}")]
	private partial void LogMalformedResponse(string command, int count, string raw);

	[LoggerMessage(1024, LogLevel.Warning, "Command {command} expected {expected} response lines but received {actual}")]
	private partial void LogUnexpectedResponseCount(string command, int expected, int actual);

	[LoggerMessage(1025, LogLevel.Debug, "Read ended after the link fell idle with {bytes} bytes received")]
	private partial void LogReadIdleTimeout(int bytes);

	[LoggerMessage(1026, LogLevel.Debug, "Discarded {bytes} stale bytes from the stream")]
	private partial void LogStreamDrained(int bytes);

	[LoggerMessage(1028, LogLevel.Debug, "Probe of amplifier {amplifier} rejected: parsed {actual} of {expected} expected lines from {raw}")]
	private partial void LogProbeRejected(int amplifier, int actual, int expected, string raw);

	[LoggerMessage(1027, LogLevel.Debug, "No echo received for {transmitted} within {timeoutMs}ms")]
	private partial void LogEchoTimeout(string transmitted, double timeoutMs);
}
