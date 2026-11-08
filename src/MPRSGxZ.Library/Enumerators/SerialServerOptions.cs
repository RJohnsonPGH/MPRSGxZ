using System.Buffers.Binary;

namespace MPRSGxZ.Library.Enumerators;

/// <summary>
/// Provides utility methods for generating serial port configuration option bytes for use with the serial server.
/// </summary>
/// <remarks>This class is intended for internal use in scenarios where serial port options must be encoded into a
/// specific byte format, including protocol-specific requirements such as magic numbers and checksums. All members are
/// static and thread-safe.</remarks>
internal static class SerialServerOptions
{
	/// <summary>
	/// Generates a byte array representing serial port configuration options based on the specified baud rate, data bits,
	/// parity, and stop bits.
	/// </summary>
	/// <remarks>The returned byte array includes a checksum and is formatted for compatibility with specific serial
	/// communication protocols.</remarks>
	/// <param name="baud">The baud rate to use for the serial port configuration.</param>
	/// <param name="dataBits">The number of data bits per byte. Defaults to eight data bits if not specified.</param>
	/// <param name="parity">The parity mode to use for error checking. Defaults to no parity if not specified.</param>
	/// <param name="stopBits">The number of stop bits to use. Defaults to one stop bit if not specified.</param>
	/// <returns>A byte array containing the encoded serial port configuration options, suitable for use with the serial server.</returns>
	internal static byte[] GetOptionBytes(Baud baud, 
		SerialServerDataBits dataBits = SerialServerDataBits.Eight, 
		SerialServerParity parity = SerialServerParity.None, 
		SerialServerStopBits stopBits = SerialServerStopBits.One)
	{
		// In order to prevent the written data to be sent to the serial device, 3 bytes of magic number 0x55AA55 are added at the beginning.
		// This will cause the serial server to apply the options without sending any data to the serial device.
		// Option byte structure: [Magic (3 bytes), Baud (3 bytes), Options (1 byte), Checksum (1 byte)]
		Span<byte> optionBytes = [0x55, 0xAA, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00];
		Span<byte> baudBytes = [0x00, 0x00, 0x00, 0x00];

		// Write the baud rate as a big-endian integer and copy the last 3 bytes to the option bytes.
		// Because the maximum baud rate is 115200 (0x0001C200), it fits within 3 bytes.
		BinaryPrimitives.WriteInt32BigEndian(baudBytes, (int)baud);
		baudBytes[1..].CopyTo(optionBytes[3..]);

		// Combine data bits, parity, and stop bits into a single options byte.
		var options = Convert.ToByte((int)dataBits | (int)parity | (int)stopBits);
		optionBytes[6] = options;

		// Calculate checksum as the sum of the baud rate bytes and options byte.
		var checksum = Convert.ToByte(optionBytes[3] + optionBytes[4] + optionBytes[5] + optionBytes[6]);
		optionBytes[7] = checksum;

		return [..optionBytes];
	}
}

// Option byte structure:
// 0b00PPPSDD
// PPP: Parity type and enabled
// S: Stop bits
// DD: Data bits

internal enum SerialServerParity
{                       // ParityType | ParityEnabled
	None  = 0b00000000, // 0bxxxxxxxx | 0b00000000
	Odd   = 0b00001000, // 0b00000000 | 0b00001000
	Even  = 0b00011000, // 0b00010000 | 0b00001000
	Mark  = 0b00101000, // 0b00100000 | 0b00001000
	Space = 0b00111000  // 0b00110000 | 0b00001000
}

internal enum SerialServerStopBits
{
	One = 0b00000000,
	Two = 0b00000100
}

internal enum SerialServerDataBits
{
	Five  = 0b00000000,
	Six   = 0b00000001,
	Seven = 0b00000010,
	Eight = 0b00000011
}
