using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MPRSGxZ.Library;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Events;
using MPRSGxZ.Library.Providers;
using MPRSGxZ.Web.Data;
using MPRSGxZ.Web.Hubs;

namespace MPRSGxZ.Web.Services;

public sealed partial class AmplifierService(ILogger<AmplifierService> logger, 
	IHubContext<AmpHub, IAmpHub> hub,
	IEndpointProvider endpointProvider,
	IDbContextFactory<AmplifierDbContext> contextFactory,
	Amplifier amplifier) : IAmplifierService
{
	private readonly SemaphoreSlim _connectSemaphore = new(1);
	private bool _zoneChangedSubscribed;

	public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
	public Amplifier Amplifier => amplifier;

	public async Task<ConnectionState> OpenAsync(CancellationToken token = default)
	{
		await _connectSemaphore.WaitAsync(token).ConfigureAwait(false);

		try
		{
			return await OpenAsyncCore(token).ConfigureAwait(false);
		}
		finally
		{
			_connectSemaphore.Release();
		}
	}

	/// <summary>
	/// Closes any existing connection and reconnects using the current connection settings from the database.
	/// Used when the user changes the connection address or port on the Connection page.
	/// </summary>
	/// <remarks>
	/// Closing is unconditional. Opening starts a polling loop whether or not it managed to connect, so state that
	/// needs releasing is not limited to a successful connection, and closing tolerates having nothing to release.
	/// Deciding from <see cref="ConnectionState"/> instead would leave the loop from a failed attempt running and
	/// polling alongside its replacement.
	/// </remarks>
	public async Task<ConnectionState> ReconnectAsync(CancellationToken token = default)
	{
		await _connectSemaphore.WaitAsync(token).ConfigureAwait(false);

		LogReconnectRequested();

		try
		{
			await Amplifier.Close().ConfigureAwait(false);

			var state = await OpenAsyncCore(token).ConfigureAwait(false);
			LogReconnectCompleted(state);

			return state;
		}
		catch (Exception ex)
		{
			// The reconnect attempt is abandoned but the app keeps running with the connection marked down.
			ConnectionState = ConnectionState.Disconnected;
			LogReconnectFailed(ex);

			throw;
		}
		finally
		{
			_connectSemaphore.Release();
		}
	}

	private async Task<ConnectionState> OpenAsyncCore(CancellationToken token)
	{
		await endpointProvider.RefreshAsync(token).ConfigureAwait(false);
		LogLoadedConnectionSettings(endpointProvider.Endpoint);

		if (!_zoneChangedSubscribed)
		{
			Amplifier.ZoneChanged += ZoneChanged;
			_zoneChangedSubscribed = true;
		}

		ConnectionState = await Amplifier.OpenAsync(token).ConfigureAwait(false);

		await ReloadZoneLinksAsync(token).ConfigureAwait(false);

		return ConnectionState;
	}

	/// <inheritdoc />
	public async Task ReloadZoneLinksAsync(CancellationToken token = default)
	{
		using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

		var links = await context.Zones
			.Where(x => x.MirrorsZoneId != null)
			.Select(x => new { PrimaryId = x.MirrorsZoneId!.Value, SecondaryId = x.Id })
			.ToListAsync(token)
			.ConfigureAwait(false);

		Amplifier.ZoneLinks.ApplyLinks(links.Select(x => (x.PrimaryId, x.SecondaryId)));

		LogLoadedZoneMirroringLinks(links.Count);
	}

	private void ZoneChanged(object? sender, ZoneChangedEventArgs e)
	{
		LogReceivedZoneChangedEvent(e.AmpId, e.ZoneId);
		hub.Clients.All.SendUpdate();
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var startupResult = await OpenAsync(cancellationToken);
		LogInitializerCompleted(startupResult);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping();
		await DisposeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		LogDisposing();
		await Amplifier.DisposeAsync();
	}

	public void Dispose()
	{
		LogDisposing();
		Amplifier.Dispose();
	}
}
