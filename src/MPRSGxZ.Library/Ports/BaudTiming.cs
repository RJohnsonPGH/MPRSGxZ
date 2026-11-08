using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Ports;

/// <summary>
/// Derives command timeouts from the negotiated baud rate.
/// </summary>
/// <remarks>
/// A fixed timeout cannot work across the supported baud range: the largest exchange (a six zone query plus its echo)
/// takes roughly 400 ms of pure line time at 9600 baud but under 20 ms at 230400. Timing out at a fixed 250 ms
/// therefore guarantees failure at the low end while being needlessly generous at the high end.
/// </remarks>
internal static class BaudTiming
{
	/// <summary>
	/// Bits transmitted per byte on the wire: 8 data bits plus a start and stop bit (8N1).
	/// </summary>
	private const int BitsPerByte = 10;

	/// <summary>
	/// Worst case bytes for a single exchange: the longest command plus its echo (<c>?1*\r\n</c> is 5 bytes, echoed
	/// back as 5) and a six line response of 27 bytes each plus the trailing prompt.
	/// </summary>
	private const int WorstCaseBytes = 10 + (6 * 27) + 1;

	/// <summary>
	/// Fixed allowance for round trip latency through the serial to Ethernet bridge and for amplifier processing time,
	/// neither of which scale with baud rate.
	/// </summary>
	private static readonly TimeSpan _fixedOverhead = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Floor applied at high baud rates, where line time becomes negligible and network latency dominates.
	/// </summary>
	private static readonly TimeSpan _minimumTimeout = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// Gets the command timeout to use at the specified baud rate.
	/// </summary>
	/// <remarks>
	/// Allows twice the theoretical line time so a single retry fits inside the budget, plus a fixed overhead, and
	/// rounds up to the next 50 ms so the values stay readable in logs.
	/// </remarks>
	internal static TimeSpan GetCommandTimeout(Baud baud)
	{
		var lineTimeMs = WorstCaseBytes * BitsPerByte * 1000.0 / (int)baud;
		var totalMs = (lineTimeMs * 2) + _fixedOverhead.TotalMilliseconds;
		var roundedMs = Math.Ceiling(totalMs / 50.0) * 50.0;

		var timeout = TimeSpan.FromMilliseconds(roundedMs);
		return timeout < _minimumTimeout ? _minimumTimeout : timeout;
	}

	/// <summary>
	/// Gets the interval at which all amplifiers can be polled without queuing work faster than the link can drain
	/// it. At low baud rates a single round of polls can exceed the nominal poll interval, so the interval has to
	/// account for the worst case cost of querying every amplifier.
	/// </summary>
	/// <param name="baud">The negotiated baud rate.</param>
	/// <param name="amplifierCount">The number of amplifiers being polled.</param>
	/// <param name="preferredInterval">The desired poll interval when the link is fast enough to sustain it.</param>
	internal static TimeSpan GetPollInterval(Baud baud, int amplifierCount, TimeSpan preferredInterval)
	{
		var required = GetCommandTimeout(baud) * Math.Max(amplifierCount, 1);
		return required > preferredInterval ? required : preferredInterval;
	}
}
