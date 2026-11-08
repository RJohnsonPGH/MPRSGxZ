namespace MPRSGxZ.Web.Logging;

/// <summary>
/// Represents a single log entry captured by <see cref="InMemoryLoggerProvider"/>.
/// </summary>
public sealed class InMemoryLogEntry
{
	public required DateTimeOffset Timestamp { get; init; }
	public required LogLevel Level { get; init; }
	public required string Category { get; init; }
	public required string Message { get; init; }
	public string? Exception { get; init; }
}
