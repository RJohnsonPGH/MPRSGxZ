namespace MPRSGxZ.Web.Logging;

/// <summary>
/// Registration for the in-memory log store that backs the Log page.
/// </summary>
public static class InMemoryLoggingExtensions
{
	/// <summary>
	/// The category prefix shared by every logger belonging to this application and its libraries.
	/// </summary>
	/// <remarks>
	/// Log categories default to the full type name of the logging class, so every type in the
	/// <c>MPRSGxZ.Web</c> and <c>MPRSGxZ.Library</c> assemblies shares this root namespace.
	/// </remarks>
	private const string ApplicationCategoryPrefix = "MPRSGxZ";

	/// <summary>
	/// Registers the <see cref="InMemoryLogStore"/> and its <see cref="InMemoryLoggerProvider"/>, restricted to log
	/// categories belonging to this application and its libraries.
	/// </summary>
	/// <remarks>
	/// The Log page exists to show what the application is doing with the amplifier, so framework noise from Entity
	/// Framework, Kestrel, the ASP.NET Core request pipeline, and SignalR would bury the entries that matter. The
	/// filter is deliberately hardcoded rather than driven from <c>appsettings.json</c>: it is a property of what this
	/// page is for, not a deployment concern, and it must not be possible to accidentally flood the in-memory store by
	/// raising a log level in configuration. Other providers (console, rolling file) are unaffected and still honour
	/// the configured <c>Logging:LogLevel</c> settings, so framework diagnostics remain available where they belong.
	/// </remarks>
	public static ILoggingBuilder AddInMemoryLogging(this ILoggingBuilder builder)
	{
		builder.Services.AddSingleton<InMemoryLogStore>();
		builder.Services.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();

		// Category is null for entries logged without one; those cannot be attributed to this application, so they
		// are dropped along with everything outside the application's root namespace.
		builder.AddFilter<InMemoryLoggerProvider>((category, _)
			=> category is not null && category.StartsWith(ApplicationCategoryPrefix, StringComparison.Ordinal));

		return builder;
	}
}
