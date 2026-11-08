using MPRSGxZ.Library.Providers;
using System.Net;

namespace MPRSGxZ.Library.IntegrationTest.Fixtures;

/// <summary>
/// Supplies a fixed endpoint from test configuration.
/// </summary>
/// <remarks>
/// <see cref="RefreshAsync"/> is a no-op because the endpoint comes from configuration that is read once at the start
/// of the run; there is nothing to re-resolve.
/// </remarks>
internal sealed class StaticEndpointProvider(IPEndPoint endpoint) : IEndpointProvider
{
	public IPEndPoint Endpoint { get; } = endpoint;

	public Task RefreshAsync(CancellationToken token = default) => Task.CompletedTask;
}
