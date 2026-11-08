namespace MPRSGxZ.Library.Exceptions;

/// <summary>
/// Thrown when attempting to link a zone that is not enabled.
/// </summary>
public class ZoneNotEnabledException : Exception
{
	public ZoneNotEnabledException(string message) : base(message)
	{
	}
}
