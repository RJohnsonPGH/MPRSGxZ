using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.IntegrationTest.Tests;

/// <summary>
/// Covers the checks that stop a malformed command from reaching the hardware.
/// </summary>
/// <remarks>
/// These need no rig and always run. They belong with the integration tests because what they protect is physical: a
/// command that renders to the wrong width shifts every field after it, and the amplifier will act on whatever that
/// happens to decode to rather than rejecting it.
/// </remarks>
public sealed class CommandGuardTests
{
	/// <summary>
	/// A set command must render as the documented fixed width frame.
	/// </summary>
	/// <remarks>
	/// The amplifier parses by position, so the two digit value is the part that matters most: a single digit value
	/// would leave the frame a character short and the amplifier would consume the terminator as data.
	/// </remarks>
	[Theory]
	[InlineData(1, ZoneIdentifier.One, 5, "<11VO05")]
	[InlineData(2, ZoneIdentifier.Three, 38, "<23VO38")]
	[InlineData(3, ZoneIdentifier.Six, 0, "<36VO00")]
	public void SetCommand_RendersAsFixedWidthFrame(int ampId, ZoneIdentifier zoneId, int value, string expected)
	{
		var command = SetCommand.Create((AmplifierIdentifier)ampId, zoneId, SetCommandType.Volume, value);

		Assert.Equal(expected, command.ToString());
	}

	/// <summary>
	/// A set command must never be addressed to every zone at once.
	/// </summary>
	/// <remarks>
	/// Zone zero is the broadcast address for queries only. Rendering it into a set would produce a frame the
	/// amplifier interprets against an unintended zone, so it is refused rather than sent.
	/// </remarks>
	[Fact]
	public void SetCommand_RejectsTheBroadcastZone()
	{
		Assert.Throws<ArgumentException>(
			() => SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.All, SetCommandType.Volume, 10));
	}

	/// <summary>
	/// Public address must be refused, because the hardware only sets it from its trigger contact.
	/// </summary>
	[Fact]
	public void SetCommand_RejectsPublicAddress()
	{
		Assert.Throws<ArgumentException>(
			() => SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.One, SetCommandType.PublicAddress, 1));
	}

	/// <summary>
	/// A value outside a property's range must be refused rather than clamped or truncated.
	/// </summary>
	/// <remarks>
	/// Both ends are checked for every settable property. A value above the range is the more dangerous of the two,
	/// since a three digit number would also break the frame width.
	/// </remarks>
	[Theory]
	[MemberData(nameof(SettableProperties))]
	public void SetCommand_RejectsValuesOutsideTheRange(string commandTypeName)
	{
		var commandType = SetCommandType.Settable.Single(x => x.CommandString == commandTypeName);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.One, commandType, commandType.MinValue - 1));

		Assert.Throws<ArgumentOutOfRangeException>(
			() => SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.One, commandType, commandType.MaxValue + 1));
	}

	/// <summary>
	/// Every settable property must accept both ends of its own range.
	/// </summary>
	/// <remarks>
	/// The companion to the rejection test: it pins the boundaries as inclusive, so a range check written with the
	/// wrong comparison is caught here rather than showing up as a value the UI offers but cannot apply.
	/// </remarks>
	[Theory]
	[MemberData(nameof(SettableProperties))]
	public void SetCommand_AcceptsBothEndsOfTheRange(string commandTypeName)
	{
		var commandType = SetCommandType.Settable.Single(x => x.CommandString == commandTypeName);

		foreach (var value in new[] { commandType.MinValue, commandType.MaxValue })
		{
			var command = SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.One, commandType, value);

			Assert.Equal($"<11{commandType.CommandString}{value:D2}", command.ToString());
		}
	}

	/// <summary>
	/// A query must render as the documented frame, and expect one line per zone it addresses.
	/// </summary>
	/// <remarks>
	/// The expected line count is what the port sizes its read against, so a broadcast query that claimed one line
	/// would leave five responses in the buffer and desynchronise everything after it.
	/// </remarks>
	[Theory]
	[InlineData(1, ZoneIdentifier.All, "?10", 6)]
	[InlineData(1, ZoneIdentifier.One, "?11", 1)]
	[InlineData(3, ZoneIdentifier.Six, "?36", 1)]
	public void QueryCommand_RendersAndDeclaresItsResponseLength(
		int ampId,
		ZoneIdentifier zoneId,
		string expected,
		int expectedLines)
	{
		var command = new QueryCommand((AmplifierIdentifier)ampId, zoneId);

		Assert.Equal(expected, command.ToString());
		Assert.Equal(expectedLines, command.ExpectedLines);
	}

	/// <summary>
	/// A set command must not expect a response.
	/// </summary>
	/// <remarks>
	/// Sets are silent, so expecting even one line would make the port consume the next command's response.
	/// </remarks>
	[Fact]
	public void SetCommand_ExpectsNoResponse()
	{
		var command = SetCommand.Create(AmplifierIdentifier.One, ZoneIdentifier.One, SetCommandType.Volume, 10);

		Assert.Equal(0, command.ExpectedLines);
	}

	public static TheoryData<string> SettableProperties()
	{
		var data = new TheoryData<string>();

		foreach (var commandType in SetCommandType.Settable)
		{
			data.Add(commandType.CommandString);
		}

		return data;
	}
}
