using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Web.Hubs;
using MPRSGxZ.Web.Services;
using MudBlazor;
using MudBlazor.Services;
using MPRSGxZ.Web.Data;
using MPRSGxZ.Library;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.Ports;
using MPRSGxZ.Library.Providers;
using MPRSGxZ.Web.Providers;
using MPRSGxZ.Web.Logging;
using NReco.Logging.File;

var builder = WebApplication.CreateBuilder(args);

// Resolve file-based resources (log file, SQLite database) relative to the running
// executable's directory rather than the current working directory, so debug runs
// (where the CWD is the project folder) still write to the build output folder.
var baseDirectory = AppContext.BaseDirectory;

builder.Logging.AddConsole();

// In-memory log store backing the Log page, capturing Information/Warning/Error entries from this
// application and its libraries only.
builder.Logging.AddInMemoryLogging();

// Log to a rolling file on disk in addition to the console and in-memory store.
// Path precedence: AMP_LOG_PATH (explicit file) > AMP_LOG_DIR (directory, default filename)
// > Logging:File:Path config > hardcoded default. This lets container deployments point
// logs at a mounted volume (e.g. /logs) via a single env var without editing appsettings.json.
var fileLoggingSection = builder.Configuration.GetSection("Logging:File");
var logFilePath = Environment.GetEnvironmentVariable("AMP_LOG_PATH");
if (string.IsNullOrWhiteSpace(logFilePath))
{
	var logFileName = Path.GetFileName(fileLoggingSection["Path"] ?? "logs/app.log");
	var logDir = Environment.GetEnvironmentVariable("AMP_LOG_DIR");
	logFilePath = !string.IsNullOrWhiteSpace(logDir)
		? Path.Combine(logDir, logFileName)
		: fileLoggingSection["Path"] ?? "logs/app.log";
}
if (!Path.IsPathRooted(logFilePath))
{
	logFilePath = Path.Combine(baseDirectory, logFilePath);
}
builder.Logging.AddFile(logFilePath, options => fileLoggingSection.Bind(options));

// Add services to the container.
// Path precedence: AMP_DB_PATH (explicit file) > AMP_DATA_DIR (directory, default filename)
// > ConnectionStrings:AmpSettingDatabase config > hardcoded default. This lets container
// deployments point the SQLite database at a mounted volume (e.g. /data) via a single env
// var without editing appsettings.json or the connection string.
var dbPathOverride = Environment.GetEnvironmentVariable("AMP_DB_PATH");
string dbConnectionString;
if (!string.IsNullOrWhiteSpace(dbPathOverride))
{
	dbConnectionString = $"Data Source={dbPathOverride}";
}
else
{
	dbConnectionString = builder.Configuration.GetConnectionString("AmpSettingDatabase")
		?? "Data Source=AmpSettings.db";
	var dataDir = Environment.GetEnvironmentVariable("AMP_DATA_DIR");
	if (!string.IsNullOrWhiteSpace(dataDir))
	{
		var dbFileName = Path.GetFileName(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(dbConnectionString).DataSource);
		dbConnectionString = $"Data Source={Path.Combine(dataDir, dbFileName)}";
	}
}
var dbConnectionStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(dbConnectionString);
if (!Path.IsPathRooted(dbConnectionStringBuilder.DataSource))
{
	dbConnectionStringBuilder.DataSource = Path.Combine(baseDirectory, dbConnectionStringBuilder.DataSource);
}
builder.Services.AddDbContextFactory<AmplifierDbContext>(options =>
		options.UseSqlite(dbConnectionStringBuilder.ConnectionString));

// When running behind a TLS-terminating reverse proxy (the expected setup for the container),
// Kestrel only sees plain HTTP from the proxy. Without forwarded headers support, NavigationManager
// (used client-side to build the SignalR hub URL) and other scheme-aware code would report the wrong
// scheme/host. Trusting all proxies/networks is safe here because the app is only ever reached through
// the configured proxy; set AMP_TRUST_PROXY_HEADERS=false to disable if exposing the container directly.
var trustProxyHeaders = !string.Equals(
	Environment.GetEnvironmentVariable("AMP_TRUST_PROXY_HEADERS"),
	"false",
	StringComparison.OrdinalIgnoreCase);
if (trustProxyHeaders)
{
	builder.Services.Configure<ForwardedHeadersOptions>(options =>
	{
		options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
		options.KnownIPNetworks.Clear();
		options.KnownProxies.Clear();
	});
}

builder.Services.AddSingleton<IEndpointProvider, MutableEndpointProvider>();
builder.Services.AddSingleton<IPort, IPAmplifierPort>();
builder.Services.AddSingleton<ZoneLinkRegistry>();
builder.Services.AddSingleton<Amplifier>();
builder.Services.AddSingleton<IAmplifierService, AmplifierService>();
builder.Services.AddHostedService<InitializerService>();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

builder.Services.AddMudServices(config =>
{
	config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
	config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;
	config.SnackbarConfiguration.VisibleStateDuration = 4000;
	config.SnackbarConfiguration.ShowTransitionDuration = 250;
	config.SnackbarConfiguration.HideTransitionDuration = 250;
	config.SnackbarConfiguration.PreventDuplicates = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AmplifierDbContext>>();
	var context = contextFactory.CreateDbContext();
	context.Database.EnsureCreated();
}

if (trustProxyHeaders)
{
	app.UseForwardedHeaders();
}

app.UseRouting();

app.MapStaticAssets();

app.MapControllers();

app.MapBlazorHub();
app.MapHub<AmpHub>("/amphub");
app.MapFallbackToPage("/_Host");

app.Run();
