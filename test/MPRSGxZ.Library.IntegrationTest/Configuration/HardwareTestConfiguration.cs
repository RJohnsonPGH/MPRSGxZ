using Microsoft.Extensions.Configuration;

namespace MPRSGxZ.Library.IntegrationTest.Configuration;

/// <summary>
/// Loads <see cref="HardwareTestOptions"/> once for the whole test run.
/// </summary>
/// <remarks>
/// Layered lowest precedence first: <c>appsettings.json</c> holds the safe defaults that are committed,
/// <c>appsettings.local.json</c> holds the machine specific rig address and is git ignored, and environment variables
/// prefixed with <c>MPRSGXZ_</c> win over both so a CI job or a one-off shell can redirect the suite without editing
/// any file.
/// </remarks>
internal static class HardwareTestConfiguration
{
	private static readonly Lazy<HardwareTestOptions> _options = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

	/// <summary>
	/// Gets the options for the current run.
	/// </summary>
	public static HardwareTestOptions Options => _options.Value;

	private static HardwareTestOptions Load()
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: true)
			.AddJsonFile("appsettings.local.json", optional: true)
			.AddEnvironmentVariables(HardwareTestOptions.EnvironmentPrefix)
			.Build();

		var options = new HardwareTestOptions();
		configuration.GetSection(HardwareTestOptions.SectionName).Bind(options);

		return options;
	}
}
