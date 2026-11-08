using Microsoft.AspNetCore.Components;

namespace MPRSGxZ.Web.Shared;

// The LoggerMessage generator requires an ILogger *field*, but Blazor can only inject into properties. The log
// methods are therefore declared static with an explicit ILogger parameter and given the injected logger at the
// call site.
public partial class MainLayout
{
	[Inject]
	private ILogger<MainLayout> Logger { get; set; } = default!;

	[LoggerMessage(7301, LogLevel.Warning, "Failed to read the saved theme preference, falling back to light mode")]
	private static partial void LogThemePreferenceReadFailed(ILogger logger, Exception ex);

	[LoggerMessage(7302, LogLevel.Error, "Failed to save the theme preference, it will not persist across sessions")]
	private static partial void LogThemePreferenceSaveFailed(ILogger logger, Exception ex);

	[LoggerMessage(7303, LogLevel.Error, "Failed to start the SignalR connection to the amplifier hub")]
	private static partial void LogHubStartFailed(ILogger logger, Exception ex);

	[LoggerMessage(7304, LogLevel.Error, "Failed to retry the SignalR connection to the amplifier hub")]
	private static partial void LogHubRetryFailed(ILogger logger, Exception ex);

	[LoggerMessage(7305, LogLevel.Debug, "Skipped a hub-triggered re-render because the layout is no longer renderable")]
	private static partial void LogHubRenderFailed(ILogger logger, Exception ex);
}
