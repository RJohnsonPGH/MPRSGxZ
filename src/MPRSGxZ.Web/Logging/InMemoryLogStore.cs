using System.Collections.Concurrent;

namespace MPRSGxZ.Web.Logging;

/// <summary>
/// Thread-safe, bounded in-memory store of recent log entries. Only Information, Warning, and Error
/// entries are retained: Critical events crash the application before they'd be useful here, and Trace/Debug
/// are too verbose for this view.
/// </summary>
public sealed class InMemoryLogStore
{
	private const int MaxEntries = 500;

	private readonly ConcurrentQueue<InMemoryLogEntry> _entries = new();

	/// <summary>
	/// Raised whenever a new entry is added, so the Log page can refresh live.
	/// </summary>
	public event Action? EntriesChanged;

	public IReadOnlyCollection<InMemoryLogEntry> Entries => [.. _entries];

	public void Add(InMemoryLogEntry entry)
	{
		_entries.Enqueue(entry);

		while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
		{
			// Trim oldest entries once the bounded capacity is exceeded.
		}

		EntriesChanged?.Invoke();
	}

	/// <summary>
	/// Drops all retained entries. Entries remain in the file log; only the in-memory view used by the
	/// Log page is cleared.
	/// </summary>
	public void Clear()
	{
		_entries.Clear();

		EntriesChanged?.Invoke();
	}
}
