using System.Diagnostics.CodeAnalysis;

namespace MPRSGxZ.Library.Commands;

/// <summary>
/// Represents a single decoded status line returned by an amplifier.
/// </summary>
/// <remarks>
/// Use <see cref="TryParse"/> to decode a line. A malformed line is a routine occurrence on a noisy or
/// desynchronised serial link, so decoding never throws: callers discard lines that fail to parse rather than
/// letting a single corrupt frame tear down the polling loop.
/// </remarks>
public sealed record CommandResponse
{
	/// <summary>
	/// The number of characters in a well formed status line.
	/// </summary>
	/// <remarks>
	/// A status line is an amplifier digit, a zone digit, and then nine two digit fields, the last of which is
	/// <see cref="Keypad"/>. Real hardware was observed returning <c>1100000000250707100100</c>, and the field list
	/// agrees with the parser used by pymonoprice, which reads eleven two digit groups.
	/// </remarks>
	public const int ResponseLength = 22;

	public string Response { get; }

	public int AmpId { get; }
	public int ZoneId { get; }
	public bool PublicAddress { get; }
	public bool Power { get; }
	public bool Mute { get; }
	public bool DoNotDisturb { get; }
	public int Volume { get; }
	public int Treble { get; }
	public int Bass { get; }
	public int Balance { get; }
	public int Source { get; }

	/// <summary>
	/// Gets a value indicating whether a wall keypad is physically connected to this zone.
	/// </summary>
	/// <remarks>
	/// Reported by the hardware only; there is no corresponding set command.
	/// </remarks>
	public bool Keypad { get; }

	private CommandResponse(
		string response,
		int ampId,
		int zoneId,
		bool publicAddress,
		bool power,
		bool mute,
		bool doNotDisturb,
		int volume,
		int treble,
		int bass,
		int balance,
		int source,
		bool keypad)
	{
		Response = response;
		AmpId = ampId;
		ZoneId = zoneId;
		PublicAddress = publicAddress;
		Power = power;
		Mute = mute;
		DoNotDisturb = doNotDisturb;
		Volume = volume;
		Treble = treble;
		Bass = bass;
		Balance = balance;
		Source = source;
		Keypad = keypad;
	}

	/// <summary>
	/// Attempts to decode a status line.
	/// </summary>
	/// <param name="response">The candidate line, already stripped of framing characters.</param>
	/// <param name="result">The decoded response, or <see langword="null"/> if the line is malformed.</param>
	/// <returns><see langword="true"/> if the line was well formed; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(string? response, [NotNullWhen(true)] out CommandResponse? result)
	{
		result = null;

		if (response is null || response.Length != ResponseLength)
		{
			return false;
		}

		// Every character must be a digit; anything else means the frame is corrupt or we are reading at the wrong
		// baud rate. Checking up front means the field reads below cannot fail.
		foreach (var character in response)
		{
			if (!char.IsAsciiDigit(character))
			{
				return false;
			}
		}

		var ampId = response[0] - '0';
		var zoneId = response[1] - '0';

		// Amplifiers are addressed 1-3 and zones 1-6. A value outside those ranges means the line is misaligned,
		// which would otherwise produce an out of range index when the response is applied to a zone.
		if (ampId is < 1 or > 3 || zoneId is < 1 or > 6)
		{
			return false;
		}

		result = new CommandResponse(
			response,
			ampId,
			zoneId,
			TwoDigits(response, 2) == 1,
			TwoDigits(response, 4) == 1,
			TwoDigits(response, 6) == 1,
			TwoDigits(response, 8) == 1,
			TwoDigits(response, 10),
			TwoDigits(response, 12),
			TwoDigits(response, 14),
			TwoDigits(response, 16),
			TwoDigits(response, 18),
			TwoDigits(response, 20) == 1);

		return true;
	}

	private static int TwoDigits(string response, int offset)
		=> ((response[offset] - '0') * 10) + (response[offset + 1] - '0');

	public override string ToString() =>
		$"{AmpId:D1}{ZoneId:D1}{(PublicAddress ? 1 : 0):D2}{(Power ? 1 : 0):D2}{(Mute ? 1 : 0):D2}{(DoNotDisturb ? 1 : 0):D2}{Volume:D2}{Treble:D2}{Bass:D2}{Balance:D2}{Source:D2}{(Keypad ? 1 : 0):D2}";

	public static implicit operator string(CommandResponse response) => response.ToString();
}
