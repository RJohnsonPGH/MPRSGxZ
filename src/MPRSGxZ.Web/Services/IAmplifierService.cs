using MPRSGxZ.Library;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Web.Services;

public interface IAmplifierService : IAsyncDisposable, IDisposable, IHostedService
{
	ConnectionState ConnectionState { get; }
	Amplifier Amplifier { get; }

	public Task<ConnectionState> OpenAsync(CancellationToken token = default);

	/// <summary>
	/// Closes any existing connection and reconnects using the current connection settings from the database.
	/// </summary>
	public Task<ConnectionState> ReconnectAsync(CancellationToken token = default);

	/// <summary>
	/// Reloads the primary/secondary zone mirroring links from the database and applies them to
	/// the underlying <see cref="Amplifier"/>. Call this after zone link configuration changes.
	/// </summary>
	public Task ReloadZoneLinksAsync(CancellationToken token = default);
}
