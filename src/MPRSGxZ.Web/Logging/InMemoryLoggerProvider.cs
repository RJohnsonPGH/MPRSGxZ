using System.Collections.Concurrent;

namespace MPRSGxZ.Web.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that captures Information, Warning, and Error log entries into an
/// <see cref="InMemoryLogStore"/> for display on the Log page. Critical entries are excluded because they
/// crash the application, and Trace/Debug are excluded because they are too verbose for this view.
/// </summary>
/// <remarks>
/// Which log categories reach this provider is decided by the filter applied in
/// <see cref="InMemoryLoggingExtensions.AddInMemoryLogging"/>, which restricts it to this application and its
/// libraries. Register this provider through that method rather than directly, or the Log page will fill with
/// framework noise.
/// </remarks>
public sealed class InMemoryLoggerProvider(InMemoryLogStore store) : ILoggerProvider
{
	private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new();

	public ILogger CreateLogger(string categoryName)
		=> _loggers.GetOrAdd(categoryName, name => new InMemoryLogger(name, store));

	public void Dispose() => _loggers.Clear();

	private sealed class InMemoryLogger(string category, InMemoryLogStore store) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel)
			=> logLevel is LogLevel.Information or LogLevel.Warning or LogLevel.Error;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			store.Add(new InMemoryLogEntry
			{
				Timestamp = DateTimeOffset.Now,
				Level = logLevel,
				Category = category,
				Message = formatter(state, exception),
				Exception = exception?.ToString(),
			});
		}
	}
}
