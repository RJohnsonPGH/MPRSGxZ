using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPRSGxZ.CLI.Commands;
using MPRSGxZ.CLI.Internal;
using MPRSGxZ.CLI.Providers;
using MPRSGxZ.Library;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.Ports;
using MPRSGxZ.Library.Providers;
using Spectre.Console.Cli;
using System.Threading;

var cancellationTokenSource = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cancellationTokenSource.Cancel();
};

var services = new ServiceCollection();

services.AddLogging(builder =>
{
	builder.SetMinimumLevel(LogLevel.Trace);
	builder.AddDebug();
});

services.AddSingleton<IEndpointProvider, CommandLineEndpointProvider>();
services.AddSingleton<IPort, IPAmplifierPort>();
services.AddSingleton<ZoneLinkRegistry>();
services.AddSingleton<Amplifier>();

var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
	config.Settings.ApplicationName = "MPRSGxZ.Cli";

	config.PropagateExceptions();

	config.AddCommand<MonitorCommand>("monitor")
		.WithDescription("Monitor the status of an amplifier.")
		.WithExample("monitor --address <IP_ADDRESS/FQDN> [--port <PORT>]");
});

await app.RunAsync(args, cancellationTokenSource.Token);
return 0;
