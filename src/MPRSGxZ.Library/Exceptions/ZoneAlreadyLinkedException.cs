namespace MPRSGxZ.Library.Exceptions;

/// <summary>
/// Thrown when attempting to link a zone that is already linked (either as a primary or a
/// secondary) in a way that would conflict with its existing link.
/// </summary>
public class ZoneAlreadyLinkedException(string message) : Exception(message)
{
}
