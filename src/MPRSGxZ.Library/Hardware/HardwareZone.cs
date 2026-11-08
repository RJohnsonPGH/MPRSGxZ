using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Hardware;

public class HardwareZone(int id)
{
	public int Id => id;

	public event EventHandler<ZonePropertyChangedEventArgs>? PropertyChanged;

	public readonly AmplifierIdentifier AmpId = id switch
	{
		<= 0 => throw new ArgumentException(nameof(id)),
		<= 6 => AmplifierIdentifier.One,
		<= 12 => AmplifierIdentifier.Two,
		<= 18 => AmplifierIdentifier.Three,
		_ => throw new ArgumentException(nameof(id))
	};

	public readonly ZoneIdentifier ZoneId = (id % 6) switch
	{
		1 => ZoneIdentifier.One,
		2 => ZoneIdentifier.Two,
		3 => ZoneIdentifier.Three,
		4 => ZoneIdentifier.Four,
		5 => ZoneIdentifier.Five,
		0 => ZoneIdentifier.Six,
		_ => throw new ArgumentException(nameof(id))
	};

	private void OnPropertyChanged(SetCommandType commandType, object newValue)
	{
		PropertyChanged?.Invoke(this, new ZonePropertyChangedEventArgs(commandType, newValue));
	}

	/// <summary>
	/// Helper method to handle property setting with change notification.
	/// Supports optional value clamping for numeric properties.
	/// </summary>
	/// <typeparam name="T">The type of the property value.</typeparam>
	/// <param name="backingField">Reference to the backing field.</param>
	/// <param name="value">The new value to set.</param>
	/// <param name="commandType">The command type to use in the change notification.</param>
	/// <param name="clamp">Optional function to clamp the value (e.g., for numeric ranges).</param>
	private void SetProperty<T>(ref T backingField, T value, SetCommandType commandType, Func<T, T>? clamp = null) where T : notnull
	{
		var newValue = clamp != null ? clamp(value) : value;
		if (!EqualityComparer<T>.Default.Equals(backingField, newValue))
		{
			backingField = newValue;
			OnPropertyChanged(commandType, newValue);
		}
	}

	private bool _power;
	public bool Power
	{
		get => _power;
		set => SetProperty(ref _power, value, SetCommandType.Power);
	}

	private bool _mute;
	public bool Mute
	{
		get => _mute;
		set => SetProperty(ref _mute, value, SetCommandType.Mute);
	}

	private bool _publicAddress;

	/// <summary>
	/// Gets a value indicating whether the amplifier is currently in public address mode.
	/// </summary>
	/// <remarks>
	/// This is an amplifier-wide state driven by the hardware PA trigger contact rather than a per-zone setting, so it
	/// is read-only and is only ever updated from a status response. Every zone on the same amplifier reports the same
	/// value.
	/// </remarks>
	public bool PublicAddress => _publicAddress;

	private bool _keypad;

	/// <summary>
	/// Gets a value indicating whether a wall keypad is physically connected to this zone.
	/// </summary>
	/// <remarks>
	/// This reports how the zone is wired rather than how it is configured, so there is no command to change it and
	/// it is only ever updated from a status response. It normally stays constant for the life of a connection, but
	/// is refreshed on every poll so that a keypad added or removed while running is still reflected.
	/// </remarks>
	public bool Keypad => _keypad;

	private bool _doNotDisturb;
	public bool DoNotDisturb
	{
		get => _doNotDisturb;
		set => SetProperty(ref _doNotDisturb, value, SetCommandType.DoNotDisturb);
	}

	private int _volume;
	public int Volume
	{
		get => _volume;
		set => SetProperty(ref _volume, value, SetCommandType.Volume, SetCommandType.Volume.Clamp);
	}

	private int _treble;
	public int Treble
	{
		get => _treble;
		set => SetProperty(ref _treble, value, SetCommandType.Treble, SetCommandType.Treble.Clamp);
	}

	private int _bass;
	public int Bass
	{
		get => _bass;
		set => SetProperty(ref _bass, value, SetCommandType.Bass, SetCommandType.Bass.Clamp);
	}

	private int _balance;
	public int Balance
	{
		get => _balance;
		set => SetProperty(ref _balance, value, SetCommandType.Balance, SetCommandType.Balance.Clamp);
	}

	private int _source = 1;
	public int Source
	{
		get => _source;
		set => SetProperty(ref _source, value, SetCommandType.Source, SetCommandType.Source.Clamp);
	}

	/// <summary>
	/// The set of properties that are mirrored from a primary zone to its secondaries.
	/// </summary>
	/// <remarks>
	/// Derived from <see cref="SetCommandType.Mirrored"/> so this cannot drift out of step with the command registry
	/// when a command type is added or its mirroring behaviour changes.
	/// </remarks>
	internal static IReadOnlyList<SetCommandType> MirroredCommandTypes => SetCommandType.Mirrored;

	/// <summary>
	/// Reads and writes the backing field for each mirrored property, keyed by command type. Holding the accessors
	/// alongside the registry avoids a hand maintained switch that could silently omit a newly mirrored property.
	/// </summary>
	private static readonly Dictionary<SetCommandType, (Func<HardwareZone, int> Get, Action<HardwareZone, int> Set)> _mirroredAccessors = new()
	{
		[SetCommandType.Power] = (z => z._power ? 1 : 0, (z, v) => z._power = v != 0),
		[SetCommandType.Mute] = (z => z._mute ? 1 : 0, (z, v) => z._mute = v != 0),
		[SetCommandType.Volume] = (z => z._volume, (z, v) => z._volume = v),
		[SetCommandType.Source] = (z => z._source, (z, v) => z._source = v),
		[SetCommandType.Treble] = (z => z._treble, (z, v) => z._treble = v),
		[SetCommandType.Bass] = (z => z._bass, (z, v) => z._bass = v),
		[SetCommandType.Balance] = (z => z._balance, (z, v) => z._balance = v),
		[SetCommandType.DoNotDisturb] = (z => z._doNotDisturb ? 1 : 0, (z, v) => z._doNotDisturb = v != 0)
	};

	private static (Func<HardwareZone, int> Get, Action<HardwareZone, int> Set) GetAccessors(SetCommandType commandType)
	{
		ArgumentNullException.ThrowIfNull(commandType);

		return _mirroredAccessors.TryGetValue(commandType, out var accessors)
			? accessors
			: throw new ArgumentOutOfRangeException(nameof(commandType), commandType, "Command type is not a mirrored property.");
	}

	/// <summary>
	/// Gets the current value of a mirrored property as an int (0/1 for booleans).
	/// </summary>
	internal int GetMirroredValue(SetCommandType commandType) => GetAccessors(commandType).Get(this);

	/// <summary>
	/// Applies a value received from mirroring a primary zone directly to the backing field,
	/// without raising <see cref="PropertyChanged"/> (and therefore without triggering another
	/// round of command execution or mirroring).
	/// </summary>
	internal void ApplyMirroredValue(SetCommandType commandType, int value)
		=> GetAccessors(commandType).Set(this, value);

	internal bool UpdateFromResponse(CommandResponse response)
	{
		bool changed = false;

		if (_publicAddress != response.PublicAddress)
		{
			_publicAddress = response.PublicAddress;
			changed = true;
		}

		if (_keypad != response.Keypad)
		{
			_keypad = response.Keypad;
			changed = true;
		}

		if (_power != response.Power)
		{
			_power = response.Power;
			changed = true;
		}

		if (_mute != response.Mute)
		{
			_mute = response.Mute;
			changed = true;
		}

		if (_doNotDisturb != response.DoNotDisturb)
		{
			_doNotDisturb = response.DoNotDisturb;
			changed = true;
		}

		if (_volume != response.Volume)
		{
			_volume = response.Volume;
			changed = true;
		}

		if (_treble != response.Treble)
		{
			_treble = response.Treble;
			changed = true;
		}

		if (_bass != response.Bass)
		{
			_bass = response.Bass;
			changed = true;
		}

		if (_balance != response.Balance)
		{
			_balance = response.Balance;
			changed = true;
		}

		if (_source != response.Source)
		{
			_source = response.Source;
			changed = true;
		}

		return changed;
	}
}
