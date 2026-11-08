using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Library.Providers;
using MPRSGxZ.Web.Data;
using System.Net;

namespace MPRSGxZ.Web.Providers;

/// <summary>
/// Provides the IP endpoint used to communicate with the amplifier. Unlike a static configuration-based
/// provider, this endpoint is resolved from the persisted connection settings and can be re-resolved at
/// runtime (e.g. when the user changes the address or port on the Connection page), allowing
/// <see cref="Services.AmplifierService"/> to reconnect to a new endpoint without restarting the application.
/// </summary>
public partial class MutableEndpointProvider(
	ILogger<MutableEndpointProvider> logger,
	IDbContextFactory<AmplifierDbContext> contextFactory) : IEndpointProvider
{
	private IPEndPoint _endpoint = new(IPAddress.Loopback, 0);

	public IPEndPoint Endpoint => _endpoint;

	/// <summary>
	/// Reads the current connection settings from the database and resolves the endpoint used for
	/// subsequent connection attempts.
	/// </summary>
	public async Task RefreshAsync(CancellationToken token = default)
	{
		using var context = await contextFactory
			.CreateDbContextAsync(token)
			.ConfigureAwait(false);
		var settings = await context
			.ConnectionSettings
			.SingleAsync(token)
			.ConfigureAwait(false);

		UpdateEndpoint(settings.Address, settings.Port);
	}

	/// <summary>
	/// Resolves the supplied address/port and updates the endpoint used for subsequent connection attempts.
	/// </summary>
	/// <param name="address">An IP address or hostname to connect to.</param>
	/// <param name="port">The port to connect to (0-65535).</param>
	private void UpdateEndpoint(string address, int port)
	{
		if (port < 0 || port > 65535)
		{
			LogInvalidPortNumber(port);
			throw new InvalidOperationException($"Port {port} is out of valid range (0-65535)");
		}

		// Try parsing as IP address first
		if (IPAddress.TryParse(address, out var ipAddress))
		{
			LogEndpointConfigured(address, port);
			_endpoint = new IPEndPoint(ipAddress, port);
			return;
		}

		// Try DNS resolution
		LogResolvingHostname(address);
		try
		{
			var addresses = Dns.GetHostAddresses(address);
			if (addresses.Length == 0)
			{
				LogHostnameNotResolved(address);
				throw new InvalidOperationException($"Hostname '{address}' could not be resolved");
			}

			var resolvedAddress = addresses[0].ToString();
			LogEndpointConfigured(resolvedAddress, port);
			_endpoint = new IPEndPoint(addresses[0], port);
		}
		catch (Exception ex)
		{
			LogDnsResolutionFailed(address, ex);
			throw;
		}
	}
}
