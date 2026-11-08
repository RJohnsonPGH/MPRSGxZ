using Microsoft.AspNetCore.Components;

namespace MPRSGxZ.Web.Pages;

// The LoggerMessage generator requires an ILogger *field*, but Blazor can only inject into properties. The log
// methods are therefore declared static with an explicit ILogger parameter and given the injected logger at the
// call site.
public partial class Zone
{
	[Inject]
	private ILogger<Zone> Logger { get; set; } = default!;

	[LoggerMessage(7201, LogLevel.Information, "Zone {zoneId} updated: name '{name}', enabled {enabled}, mirrors {mirrorsZoneId}")]
	private static partial void LogZoneUpdated(ILogger logger, int zoneId, string name, bool enabled, int? mirrorsZoneId);

	[LoggerMessage(7202, LogLevel.Error, "Failed to save changes for zone {zoneId}")]
	private static partial void LogZoneUpdateFailed(ILogger logger, Exception ex, int zoneId);

	[LoggerMessage(7203, LogLevel.Information, "Zone {zoneId} mirroring changed to primary {primaryId}")]
	private static partial void LogMirroringChanged(ILogger logger, int zoneId, int? primaryId);

	[LoggerMessage(7204, LogLevel.Error, "Failed to change mirroring for zone {zoneId}")]
	private static partial void LogMirroringChangeFailed(ILogger logger, Exception ex, int zoneId);

	[LoggerMessage(7205, LogLevel.Error, "Failed to revert zone {zoneId} to its stored values after a failed save")]
	private static partial void LogZoneRevertFailed(ILogger logger, Exception ex, int zoneId);
}
