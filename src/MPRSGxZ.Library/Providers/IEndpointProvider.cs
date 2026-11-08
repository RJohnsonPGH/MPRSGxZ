using System.Net;

namespace MPRSGxZ.Library.Providers;

public interface IEndpointProvider
{
	IPEndPoint Endpoint { get; }

	/// <summary>
	/// Re-resolves the endpoint from its underlying source (e.g. persisted connection settings),
	/// updating <see cref="Endpoint"/> for subsequent connection attempts. Implementations whose
	/// endpoint is fixed at startup (e.g. command-line arguments) may treat this as a no-op.
	/// </summary>
	Task RefreshAsync(CancellationToken token = default);
}
