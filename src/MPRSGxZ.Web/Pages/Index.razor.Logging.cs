using Microsoft.AspNetCore.Components;

namespace MPRSGxZ.Web.Pages;

// The LoggerMessage generator requires an ILogger *field*, but Blazor can only inject into properties. The log
// methods are therefore declared static with an explicit ILogger parameter and given the injected logger at the
// call site.
public partial class Index
{
	[Inject]
	private ILogger<Index> Logger { get; set; } = default!;

	[LoggerMessage(7401, LogLevel.Error, "Failed to reload the selectable zone list")]
	private static partial void LogZoneListReloadFailed(ILogger logger, Exception ex);
}
