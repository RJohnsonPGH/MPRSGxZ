using Microsoft.AspNetCore.Components;

namespace MPRSGxZ.Web.Pages;

// The LoggerMessage generator requires an ILogger *field*, but Blazor can only inject into properties. The log
// methods are therefore declared static with an explicit ILogger parameter and given the injected logger at the
// call site.
public partial class Source
{
	[Inject]
	private ILogger<Source> Logger { get; set; } = default!;

	[LoggerMessage(7101, LogLevel.Information, "Source {sourceId} updated: name '{name}', enabled {enabled}")]
	private static partial void LogSourceUpdated(ILogger logger, int sourceId, string name, bool enabled);

	[LoggerMessage(7102, LogLevel.Error, "Failed to save changes for source {sourceId}")]
	private static partial void LogSourceUpdateFailed(ILogger logger, Exception ex, int sourceId);

	[LoggerMessage(7103, LogLevel.Error, "Failed to revert source {sourceId} to its stored values after a failed save")]
	private static partial void LogSourceRevertFailed(ILogger logger, Exception ex, int sourceId);
}
