using Microsoft.Extensions.Logging;

namespace MPRSGxZ.Library.Hardware;

public sealed partial class ZoneLinkRegistry
{
	[LoggerMessage(5001, LogLevel.Warning, "Skipped invalid stored zone link: primary {primaryId}, secondary {secondaryId}")]
	private partial void LogSkippedInvalidStoredLink(Exception ex, int primaryId, int secondaryId);

	[LoggerMessage(5002, LogLevel.Information, "Applied {applied} zone mirroring link(s), skipped {skipped}")]
	private partial void LogAppliedLinks(int applied, int skipped);
}
