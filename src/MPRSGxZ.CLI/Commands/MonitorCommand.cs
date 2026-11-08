using Microsoft.Extensions.Logging;
using MPRSGxZ.CLI.Providers;
using MPRSGxZ.Library;
using MPRSGxZ.Library.Providers;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MPRSGxZ.CLI.Commands;

public sealed partial class MonitorCommand(ILogger<MonitorCommand> logger, IEndpointProvider endpointProvider, Amplifier amplifier) : AsyncCommand<MonitorCommand.Settings>
{
    public sealed class Settings : CommandSettings
	{
		[CommandOption("-a|--address", true)]
		[Description("The IP address or FQDN of the serial server.")]
		public required string Address { get; set; }

		[CommandOption("-p|--port", false)]
		[Description("The TCP port of the serial server. Default is 23.")]
		public int Port { get; set; } = 23;

		public override ValidationResult Validate()
		{
			if (Port < 1 || Port > 65535)
			{
				return ValidationResult.Error("Port must be between 1 and 65535.");
			}

			var hostNameType = Uri.CheckHostName(Address);
			if (hostNameType != UriHostNameType.Dns && 
				hostNameType != UriHostNameType.IPv4 && 
				hostNameType != UriHostNameType.IPv6)
			{
				return ValidationResult.Error("Address must be a valid IP address or FQDN.");
			}

			return ValidationResult.Success();
		}
	}

	protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
	{
		if (endpointProvider is not CommandLineEndpointProvider commandLineEndpointProvider)
		{
			LogInvalidEndpointProvider();
			throw new InvalidOperationException("Endpoint provider is not an CommandLineEndpointProvider.");
		}

		if (IPAddress.TryParse(settings.Address, out var ipAddress))
		{
			commandLineEndpointProvider.Endpoint = new IPEndPoint(ipAddress, settings.Port);
		}
		else
		{
			var addresses = await Dns.GetHostAddressesAsync(settings.Address, cancellationToken);
			if (addresses.Length == 0)
			{
				LogCouldNotResolveAddress(settings.Address);
				throw new InvalidOperationException($"Could not resolve address {settings.Address}.");
			}
			commandLineEndpointProvider.Endpoint = new IPEndPoint(addresses[0], settings.Port);
		}
		await amplifier.OpenAsync(cancellationToken);

		var table = new Table()
			.RoundedBorder()
			.ShowRowSeparators()
			.BorderColor(Color.Grey)
			.Title($"{amplifier.AmplifierCount} Amplifier(s) Detected", new Style(Color.Yellow))
			.AddColumn("Zone")
			.AddColumn("Power")
			.AddColumn("Mute")
			.AddColumn("PA")
			.AddColumn("DND")
			.AddColumn("Volume")
			.AddColumn("Treble")
			.AddColumn("Bass")
			.AddColumn("Balance")
			.AddColumn("Source")
			.AddColumn("Keypad");

		var liveTask = AnsiConsole.Live(table)
			.StartAsync(async context =>
			{
				while (true && !cancellationToken.IsCancellationRequested)
				{
					for (int i = 0; i < amplifier.AmplifierCount; i++)
					{
						for (int j = 0; j < 6; j++)
						{
							var zone = amplifier.Zones[(i * 6) + j];
							table.AddRow(
								$"Amp {i + 1} Zone {j + 1}",
								zone.Power ? "On" : "Off",
								zone.Mute ? "Muted" : "Unmuted",
								zone.PublicAddress ? "On" : "Off",
								zone.DoNotDisturb ? "On" : "Off",
								zone.Volume.ToString(),
								zone.Treble.ToString(),
								zone.Bass.ToString(),
								zone.Balance.ToString(),
								zone.Source.ToString(),
								zone.Keypad ? "Yes" : "No");
						}
					}

					context.Refresh();
					Thread.Sleep(1000);
					table.Rows.Clear();
				}
			});

		return 0;
	}
}