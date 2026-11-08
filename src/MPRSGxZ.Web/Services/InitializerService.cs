namespace MPRSGxZ.Web.Services;

public partial class InitializerService(ILogger<InitializerService> logger, IAmplifierService amplifierService) : IHostedService
{
	/// <remarks>
	/// Failing to reach the amplifier is an expected condition, not a startup error: the endpoint is user-configurable
	/// from the Connection page and the amplifier may simply be powered off. A throw here would abort host startup and
	/// take down the very UI needed to correct the endpoint, so the failure is logged and swallowed. The connection
	/// state remains disconnected and the user can retry with <see cref="IAmplifierService.ReconnectAsync"/>.
	/// </remarks>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		LogStarting();

		try
		{
			var state = await amplifierService.OpenAsync(cancellationToken).ConfigureAwait(false);
			LogStarted(state);
		}
		catch (Exception ex)
		{
			LogStartFailed(ex);
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping();

		try
		{
			await amplifierService.DisposeAsync();
			LogStopped();
		}
		catch (Exception ex)
		{
			// Shutdown is already underway; surfacing this would only mask the real reason for stopping.
			LogStopFailed(ex);
		}
	}
}
