using System.Net;

namespace MPRSGxZ.Library.IntegrationTest.Configuration;

/// <summary>
/// Settings that describe the physical amplifier the integration tests run against.
/// </summary>
/// <remarks>
/// Bound from <c>appsettings.json</c>, then <c>appsettings.local.json</c> (git ignored, for machine specific
/// addresses), then environment variables prefixed with <c>MPRSGXZ_</c>. Every test in this project is skipped unless
/// <see cref="Enabled"/> is true, so a checkout with no hardware attached still produces a green build.
/// </remarks>
public sealed class HardwareTestOptions
{
	/// <summary>
	/// The configuration section these options are bound from.
	/// </summary>
	public const string SectionName = "HardwareTests";

	/// <summary>
	/// The environment variable prefix that overrides any file supplied value, for example
	/// <c>MPRSGXZ_HardwareTests__Host</c>.
	/// </summary>
	public const string EnvironmentPrefix = "MPRSGXZ_";

	/// <summary>
	/// Gets or sets a value indicating whether hardware tests should run at all. Defaults to false so that the suite
	/// is inert until someone deliberately points it at a rig.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether tests are permitted to change amplifier state. Tests that write always
	/// capture the affected zone's state first and restore it afterwards, but that is still audible, so it is opt in
	/// separately from <see cref="Enabled"/>.
	/// </summary>
	public bool AllowMutation { get; set; }

	/// <summary>
	/// Gets or sets the host name or IP address of the serial to Ethernet bridge in front of the amplifier.
	/// </summary>
	public string Host { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the TCP port the bridge listens on.
	/// </summary>
	public int Port { get; set; } = 23;

	/// <summary>
	/// Gets or sets the number of amplifiers physically present on the RS-232 chain. Discovery is asserted against
	/// this, so it must match the rig.
	/// </summary>
	public int ExpectedAmplifierCount { get; set; } = 1;

	/// <summary>
	/// Gets or sets the zone used by tests that need to write. Expressed in flat 1-18 numbering, matching
	/// <see cref="Amplifier.Zones"/>. Pick a zone with nothing connected to it, or one you do not mind hearing.
	/// </summary>
	public int MutableZoneId { get; set; } = 1;

	/// <summary>
	/// Gets or sets the number of seconds any single test may take before it is abandoned. Guards against a wedged
	/// bridge hanging the run.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the number of milliseconds to wait after a write before reading the value back. The amplifier
	/// applies a set command asynchronously, so an immediate read can still report the previous value.
	/// </summary>
	public int SettleMilliseconds { get; set; } = 500;

	/// <summary>
	/// Gets the configured endpoint.
	/// </summary>
	/// <exception cref="InvalidOperationException">The host is missing or cannot be resolved.</exception>
	public IPEndPoint GetEndpoint()
	{
		if (string.IsNullOrWhiteSpace(Host))
		{
			throw new InvalidOperationException(
				$"{SectionName}:{nameof(Host)} is not configured. Set it in appsettings.local.json or via {EnvironmentPrefix}{SectionName}__{nameof(Host)}.");
		}

		// A rig is just as likely to be referred to by name as by address, so accept either.
		if (!IPAddress.TryParse(Host, out var address))
		{
			var resolved = Dns.GetHostAddresses(Host);
			address = resolved.Length > 0
				? resolved[0]
				: throw new InvalidOperationException($"'{Host}' could not be resolved to an IP address.");
		}

		return new IPEndPoint(address, Port);
	}

	/// <summary>
	/// Gets the reason the hardware tests cannot run, or <see langword="null"/> if they can.
	/// </summary>
	/// <remarks>
	/// Returned as a string rather than a bool so the skip message can explain precisely which knob is off, which
	/// is otherwise a frustrating thing to diagnose from a silently skipped test.
	/// </remarks>
	public string? GetSkipReason()
	{
		if (!Enabled)
		{
			return $"Hardware tests are disabled. Set {SectionName}:{nameof(Enabled)} to true to run them.";
		}

		if (string.IsNullOrWhiteSpace(Host))
		{
			return $"Hardware tests are enabled but {SectionName}:{nameof(Host)} is not configured.";
		}

		return null;
	}

	/// <summary>
	/// Gets the reason a state changing test cannot run, or <see langword="null"/> if it can.
	/// </summary>
	public string? GetMutationSkipReason()
		=> GetSkipReason()
			?? (AllowMutation
				? null
				: $"State changing tests are disabled. Set {SectionName}:{nameof(AllowMutation)} to true to run them.");

	/// <summary>
	/// Gets the per test timeout.
	/// </summary>
	public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

	/// <summary>
	/// Gets the delay allowed between writing a value and reading it back.
	/// </summary>
	public TimeSpan SettleDelay => TimeSpan.FromMilliseconds(SettleMilliseconds);
}
