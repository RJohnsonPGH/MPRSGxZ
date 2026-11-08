namespace MPRSGxZ.Web.Providers;

public partial class MutableEndpointProvider
{
	[LoggerMessage(3001, LogLevel.Error, "Port number {port} is out of valid range (0-65535)")]
	private partial void LogInvalidPortNumber(int port);

	[LoggerMessage(3002, LogLevel.Information, "Endpoint configured: {address}:{port}")]
	private partial void LogEndpointConfigured(string address, int port);

	[LoggerMessage(3003, LogLevel.Information, "Resolving hostname: {hostname}")]
	private partial void LogResolvingHostname(string hostname);

	[LoggerMessage(3004, LogLevel.Error, "Hostname '{hostname}' could not be resolved to any IP address")]
	private partial void LogHostnameNotResolved(string hostname);

	[LoggerMessage(3005, LogLevel.Error, "DNS resolution failed for hostname '{hostname}'")]
	private partial void LogDnsResolutionFailed(string hostname, Exception ex);
}
