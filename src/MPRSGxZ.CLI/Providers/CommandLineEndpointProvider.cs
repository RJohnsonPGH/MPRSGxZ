using MPRSGxZ.Library.Providers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MPRSGxZ.CLI.Providers;

public class CommandLineEndpointProvider : IEndpointProvider
{
    public IPEndPoint Endpoint { get; set; } = default!;

    // The endpoint is fixed from command-line arguments at startup, so there is nothing to re-resolve.
    public Task RefreshAsync(CancellationToken token = default) => Task.CompletedTask;
}
