namespace MPRSGxZ.Library.Exceptions;

/// <summary>
/// Thrown when attempting to unlink a primary/secondary zone pair that is not currently linked.
/// </summary>
public class ZoneNotLinkedException(string message) : Exception(message)
{
}
