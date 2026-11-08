using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Commands;

/// <summary>
/// Represents a command that writes a single property value to one zone of one amplifier.
/// </summary>
/// <remarks>
/// Instances can only be produced through <see cref="Create"/>, which validates the target zone, the command type and
/// the value before any bytes reach the wire. This is deliberately a class rather than a record: a record's generated
/// copy constructor would allow <c>with</c> expressions to bypass validation entirely.
/// </remarks>
internal sealed class SetCommand : ICommand
{
	/// <summary>
	/// Gets the identifier of the amplifier the command targets.
	/// </summary>
	public AmplifierIdentifier AmpId { get; }

	/// <summary>
	/// Gets the identifier of the zone the command targets.
	/// </summary>
	public ZoneIdentifier ZoneId { get; }

	/// <summary>
	/// Gets the property being written.
	/// </summary>
	public SetCommandType CommandType { get; }

	/// <summary>
	/// Gets the value being written. Always within the command type's valid range.
	/// </summary>
	public int Value { get; }

	public int ExpectedLines => 0;

	private SetCommand(AmplifierIdentifier ampId, ZoneIdentifier zoneId, SetCommandType commandType, int value)
	{
		AmpId = ampId;
		ZoneId = zoneId;
		CommandType = commandType;
		Value = value;
	}

	/// <summary>
	/// Creates a validated set command.
	/// </summary>
	/// <param name="ampId">The amplifier to target.</param>
	/// <param name="zoneId">The zone to target. <see cref="ZoneIdentifier.All"/> is not accepted.</param>
	/// <param name="commandType">The property to write. Must be settable.</param>
	/// <param name="value">The value to write. Must be within the command type's range.</param>
	/// <exception cref="ArgumentException">
	/// The zone is <see cref="ZoneIdentifier.All"/>, or the command type is read-only.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">The value is outside the command type's valid range.</exception>
	public static SetCommand Create(AmplifierIdentifier ampId, ZoneIdentifier zoneId, SetCommandType commandType, int value)
	{
		ArgumentNullException.ThrowIfNull(commandType);

		// The hardware rejects a broadcast set; only queries may target all zones.
		if (zoneId == ZoneIdentifier.All)
		{
			throw new ArgumentException("Set commands cannot target all zones.", nameof(zoneId));
		}

		if (!commandType.IsSettable)
		{
			throw new ArgumentException($"The {commandType} property is reported by the hardware but cannot be set.", nameof(commandType));
		}

		if (!commandType.IsInRange(value))
		{
			throw new ArgumentOutOfRangeException(
				nameof(value),
				value,
				$"The {commandType} property accepts values between {commandType.MinValue} and {commandType.MaxValue}.");
		}

		return new SetCommand(ampId, zoneId, commandType, value);
	}

	public override string ToString() => $"<{(int)AmpId}{(int)ZoneId}{CommandType}{Value:D2}";

	public static implicit operator string(SetCommand command) => command.ToString();
}
