using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;

namespace MPRSGxZ.Library.Ports;

public interface IPort : IDisposable, IAsyncDisposable
{
    internal ConnectionState ConnectionState { get; }

    /// <summary>
    /// Gets the timeout appropriate for a single command exchange on this port, derived from the negotiated link
    /// speed. Callers should use this instead of a hard coded value so that a slow link is not mistaken for a
    /// failed one.
    /// </summary>
    internal TimeSpan CommandTimeout { get; }

    /// <summary>
    /// Gets the interval at which the given number of amplifiers can be polled without queuing requests faster than
    /// the link can serve them.
    /// </summary>
    /// <param name="amplifierCount">The number of amplifiers to poll each round.</param>
    /// <param name="preferredInterval">The desired interval when the link is fast enough to sustain it.</param>
    internal TimeSpan GetPollInterval(int amplifierCount, TimeSpan preferredInterval);

    Task<ConnectionState> OpenAsync(CancellationToken token);
    Task CloseAsync();
    Task<CommandResult> ExecuteCommandAsync(ICommand command, CancellationToken token);
    Task<int> GetAmplifierCountAsync(CancellationToken token);
}