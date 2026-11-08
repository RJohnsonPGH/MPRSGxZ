namespace MPRSGxZ.Library.Commands;

/// <summary>
/// Represents a command type that can be exchanged with a device, including its command string identifier and valid
/// value range.
/// </summary>
/// <remarks>
/// This is a closed set: instances cannot be created outside this class, so every value that reaches the wire is one of
/// the predefined types below. Use the static properties, such as <see cref="Power"/>, <see cref="Mute"/>, or
/// <see cref="Volume"/>, to access them, and <see cref="All"/>, <see cref="Mirrored"/> or <see cref="TryParse"/> to
/// discover them dynamically.
///
/// Because instances are singletons, reference equality is sufficient for comparison.
/// </remarks>
public sealed class SetCommandType
{
	/// <summary>
	/// Gets the string identifier used to represent the command when communicating with the device.
	/// </summary>
	public string CommandString { get; }

	/// <summary>
	/// Gets the minimum valid value that can be assigned to this command type.
	/// </summary>
	public int MinValue { get; }

	/// <summary>
	/// Gets the maximum valid value that can be assigned to this command type.
	/// </summary>
	public int MaxValue { get; }

	/// <summary>
	/// Gets a value indicating whether this property is propagated from a primary zone to its secondaries.
	/// </summary>
	public bool IsMirrored { get; }

	/// <summary>
	/// Gets a value indicating whether this property can be written to the amplifier. Read-only properties are
	/// reported by the hardware in status responses but cannot be the subject of a set command.
	/// </summary>
	public bool IsSettable { get; }

	private SetCommandType(string commandString, int minValue, int maxValue, bool isMirrored = true, bool isSettable = true)
	{
		CommandString = commandString;
		MinValue = minValue;
		MaxValue = maxValue;
		IsMirrored = isMirrored;
		IsSettable = isSettable;
	}

	/// <summary>
	/// Gets the command type used to set the power state.
	/// </summary>
	public static SetCommandType Power { get; } = new(@"PR", 0, 1);

	/// <summary>
	/// Gets the command type used to set the mute state.
	/// </summary>
	public static SetCommandType Mute { get; } = new(@"MU", 0, 1);

	/// <summary>
	/// Gets the command type used to set the volume level.
	/// </summary>
	public static SetCommandType Volume { get; } = new(@"VO", 0, 38);

	/// <summary>
	/// Gets the command type used to adjust the treble level.
	/// </summary>
	public static SetCommandType Treble { get; } = new(@"TR", 0, 14);

	/// <summary>
	/// Gets the command type used to adjust the bass level.
	/// </summary>
	public static SetCommandType Bass { get; } = new(@"BS", 0, 14);

	/// <summary>
	/// Gets the command type used to adjust the left/right balance, where 10 is centred.
	/// </summary>
	public static SetCommandType Balance { get; } = new(@"BL", 0, 20);

	/// <summary>
	/// Gets the command type that represents the input source.
	/// </summary>
	public static SetCommandType Source { get; } = new(@"CH", 1, 6);

	/// <summary>
	/// Gets the command type used to set the do not disturb state.
	/// </summary>
	public static SetCommandType DoNotDisturb { get; } = new(@"DT", 0, 1);

	/// <summary>
	/// Gets the command type representing the public address state.
	/// </summary>
	/// <remarks>
	/// This is an amplifier-wide state driven by the hardware PA trigger contact, not a per-zone setting: it is
	/// reported identically by every zone on an amplifier and cannot be written. It is therefore neither settable nor
	/// mirrored, and exists only so status responses can be described using the same vocabulary.
	/// </remarks>
	public static SetCommandType PublicAddress { get; } = new(@"PA", 0, 1, isMirrored: false, isSettable: false);

	/// <summary>
	/// Gets every known command type.
	/// </summary>
	public static IReadOnlyList<SetCommandType> All { get; } =
	[
		Power,
		Mute,
		Volume,
		Treble,
		Bass,
		Balance,
		Source,
		DoNotDisturb,
		PublicAddress
	];

	/// <summary>
	/// Gets the command types that can be written to the amplifier.
	/// </summary>
	public static IReadOnlyList<SetCommandType> Settable { get; } = [.. All.Where(x => x.IsSettable)];

	/// <summary>
	/// Gets the command types that are propagated from a primary zone to its secondaries.
	/// </summary>
	public static IReadOnlyList<SetCommandType> Mirrored { get; } = [.. All.Where(x => x.IsMirrored)];

	private static readonly Dictionary<string, SetCommandType> _byCommandString =
		All.ToDictionary(x => x.CommandString, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Attempts to resolve a command type from its wire identifier.
	/// </summary>
	/// <param name="commandString">The two character identifier, for example "VO".</param>
	/// <param name="commandType">When this method returns, contains the resolved command type, if found.</param>
	/// <returns>true if the identifier matched a known command type; otherwise, false.</returns>
	public static bool TryParse(string commandString, out SetCommandType commandType)
	{
		if (commandString is null)
		{
			commandType = default!;
			return false;
		}

		return _byCommandString.TryGetValue(commandString, out commandType!);
	}

	/// <summary>
	/// Clamps a value to the valid range for this command type.
	/// </summary>
	/// <param name="value">The value to clamp.</param>
	/// <returns>The value clamped to the [MinValue, MaxValue] range.</returns>
	public int Clamp(int value) => Math.Clamp(value, MinValue, MaxValue);

	/// <summary>
	/// Determines whether the supplied value is within the valid range for this command type.
	/// </summary>
	public bool IsInRange(int value) => value >= MinValue && value <= MaxValue;

	public override string ToString() => CommandString;

	public static implicit operator string(SetCommandType commandType) => commandType.CommandString;
}
