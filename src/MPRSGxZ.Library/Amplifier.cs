using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Events;
using MPRSGxZ.Library.Hardware;
using MPRSGxZ.Library.Ports;
using System.Net.Sockets;

namespace MPRSGxZ.Library;

/// <summary>
/// Orchestrates communication with a Marantz amplifier system via RS-232 serial protocol.
/// Manages amplifier discovery, zone configuration, polling, and command execution.
/// </summary>
/// <remarks>
/// This class provides a facade over the IPort communication abstraction and manages
/// an array of HardwareZone objects representing the 18 zones (3 amplifiers � 6 zones each).
/// 
/// The Amplifier class:
/// - Establishes and maintains connection to the amplifier hardware
/// - Discovers the number of available amplifiers
/// - Attaches property change listeners to zones for command execution
/// - Polls amplifiers periodically to synchronize zone state
/// - Handles zone update events from the hardware
/// 
/// Communication is fully asynchronous and cancellation-aware.
/// </remarks>
public sealed partial class Amplifier(ILogger<Amplifier> logger, IPort port, ZoneLinkRegistry zoneLinks) : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Gets the number of amplifiers discovered during the last successful connection.
	/// </summary>
	/// <value>The count of amplifiers (typically 1-3). Zero if not yet connected.</value>
	public int AmplifierCount { get; private set; }

	/// <summary>
	/// Gets the array of 18 hardware zones (6 zones per amplifier � 3 amplifiers).
	/// </summary>
	/// <remarks>
	/// Zones are indexed 1-18, where zones 1-6 belong to amplifier 1, 7-12 to amplifier 2, etc.
	/// Each zone provides properties for controlling power, volume, mute, treble, bass, balance, and source.
	/// </remarks>
	public HardwareZone[] Zones { get; private set; } = [.. Enumerable.Range(1, 18).Select(x => new HardwareZone(x))];

	/// <summary>
	/// Tracks which zones mirror which other zones (primary/secondary relationships).
	/// </summary>
	/// <remarks>
	/// Consumers (e.g. the web layer) configure links here based on persisted configuration.
	/// Whenever a primary zone's property changes - whether from a local command or from a poll
	/// response reflecting a physical remote - the same value is propagated to its secondaries.
	/// </remarks>
	public ZoneLinkRegistry ZoneLinks { get; } = zoneLinks;

	/// <summary>
	/// Raised when a zone's state changes as reported by the amplifier during polling.
	/// </summary>
	/// <remarks>
	/// This event fires when poll responses indicate a zone property has changed
	/// (e.g., from hardware buttons or external control). It allows consumers to update UI.
	/// </remarks>
	public EventHandler<ZoneChangedEventArgs> ZoneChanged { get; set; } = default!;

	/// <summary>
	/// Raised when an operation fails (command execution error, timeout, or communication failure).
	/// </summary>
	/// <remarks>
	/// Contains a descriptive message explaining what failed. Used to update UI status indicators.
	/// </remarks>
	public EventHandler<AmplifierStatusEventArgs> AmplifierStatusChanged { get; set; } = default!;

	/// <summary>
	/// The number of zones each amplifier exposes, including the two that a four zone model reports but does not have.
	/// </summary>
	private const int ZonesPerAmplifier = 6;

	/// <summary>
	/// The desired poll interval. The effective interval may be longer on a slow link.
	/// </summary>
	private static readonly TimeSpan PreferredPollInterval = TimeSpan.FromMilliseconds(250);

	private PeriodicTimer _pollTimer = new(PreferredPollInterval);
	private Task _pollTask = default!;
	private CancellationTokenSource _pollTokenSource = new();
	private bool _zoneHandlersAttached;

	private void Zone_PropertyChanged(object? sender, ZonePropertyChangedEventArgs e)
	{
		if (sender is not HardwareZone zone)
			return;

		// The Amplifier orchestrates the command execution based on zone property changes. The property setter that
		// raised this event is synchronous and cannot await the command, so the task is deliberately not awaited;
		// a continuation observes any fault so it is logged rather than discarded as an unobserved task exception.
		_ = HandleZonePropertyChangeAsync(zone, e.SetCommandType, e.NewValue)
			.ContinueWith(
				task => LogUnhandledZoneCommandFault(task.Exception!, zone.Id, e.SetCommandType.ToString()),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
	}

	/// <summary>
	/// Raises <see cref="AmplifierStatusChanged"/> if anything is subscribed.
	/// </summary>
	/// <remarks>
	/// The event is declared as a settable property initialised to <see langword="default"/>, so it is null until a
	/// consumer assigns it. Invoking it directly therefore throws for any consumer that does not subscribe, which is
	/// exactly the failure path this is used to report.
	/// </remarks>
	private void RaiseStatus(string message)
		=> AmplifierStatusChanged?.Invoke(this, new AmplifierStatusEventArgs(message));

	/// <summary>
	/// Handles a property change in a zone by executing the corresponding command on the amplifier.
	/// </summary>
	/// <param name="zone">The zone whose property changed.</param>
	/// <param name="commandType">The type of command to execute (e.g., Power, Volume, Mute).</param>
	/// <param name="newValue">The new value for the property.</param>
	/// <remarks>
	/// This method is called by the Zone_PropertyChanged event handler and:
	/// 1. Converts boolean properties to 0/1 integers for the command protocol
	/// 2. Creates a SetCommand with the zone, amplifier, and new value
	/// 3. Sends the command to the amplifier with a 250ms timeout
	/// 4. Raises AmplifierStatusChanged event on failure
	/// 
	/// Errors are logged but not re-thrown, allowing other operations to continue.
	/// </remarks>
	private async Task HandleZonePropertyChangeAsync(HardwareZone zone, SetCommandType commandType, object newValue)
	{
		// Convert boolean values (Power, Mute, etc.) to 0/1 integers for the RS-232 protocol. The value arrives boxed
		// as object, so an unexpected type is rejected explicitly rather than through an unboxing cast that would
		// throw an InvalidCastException from inside a fire and forget task.
		int value;
		switch (newValue)
		{
			case bool b:
				value = b ? 1 : 0;
				break;
			case int i:
				value = i;
				break;
			default:
				LogUnsupportedPropertyValue(zone.Id, commandType.ToString(), newValue?.GetType().Name ?? "null");
				RaiseStatus("Failed to execute command due to an unsupported value type.");
				return;
		}

		await SendSetCommandAsync(zone, commandType, value).ConfigureAwait(false);

		// If this zone is a primary, propagate the same command to every secondary that mirrors
		// it. Secondaries are updated silently (no PropertyChanged) so this does not recurse.
		if (MirroredPropertyTypes.Contains(commandType))
		{
			foreach (var secondaryId in ZoneLinks.GetSecondaries(zone.Id))
			{
				var secondary = Zones[secondaryId - 1];
				secondary.ApplyMirroredValue(commandType, value);
				await SendSetCommandAsync(secondary, commandType, value).ConfigureAwait(false);
			}
		}
	}

	private static readonly HashSet<SetCommandType> MirroredPropertyTypes = [.. SetCommandType.Mirrored];

	/// <summary>
	/// Sends a single set command for a zone and reports failures. Shared by direct zone property
	/// changes and mirroring fan-out to secondaries.
	/// </summary>
	private async Task SendSetCommandAsync(HardwareZone zone, SetCommandType commandType, int value)
	{
		SetCommand set;

		try
		{
			set = SetCommand.Create(zone.AmpId, zone.ZoneId, commandType, value);
		}
		catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
		{
			// An invalid command is a caller error rather than a hardware fault, so it is reported without ever
			// reaching the wire.
			LogInvalidCommand(ex, zone.Id, commandType.ToString(), value);
			RaiseStatus("Failed to execute command because it was not valid for this zone.");
			return;
		}

		try
		{
			// The timeout is derived from the negotiated baud rate: a six zone exchange takes far longer at 9600 baud
			// than at 230400, so a fixed budget would time out every command on a slow link.
			using var tokenSource = new CancellationTokenSource(port.CommandTimeout);
			var token = tokenSource.Token;

			var result = await port.ExecuteCommandAsync(set, token).ConfigureAwait(false);

			if (result.Status == CommandResultStatus.Failed)
			{
				RaiseStatus("Failed to execute command.");
				LogCommandExecutionFailed((string)set);
				return;
			}

			LogCommandExecutionSuccessful((string)set);
		}
		catch (OperationCanceledException)
		{
			// The amplifier did not answer within the budget allowed for the current baud rate.
			RaiseStatus("Failed to execute command due to timeout.");
			LogCommandExecutionTimeout((string)set);
		}
		catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
		{
			// The link dropped. Surfacing this instead of letting it escape keeps a transient network fault from
			// tearing down the caller, and lets the health loop re-establish the connection.
			LogCommandExecutionError(ex, (string)set);
			RaiseStatus("Failed to execute command due to a communication error.");
		}
	}

	/// <summary>
	/// Asynchronously opens a connection to the amplifier and initializes the system.
	/// </summary>
	/// <param name="token">A cancellation token to allow early termination of the connection attempt.</param>
	/// <returns>
	/// A ConnectionState value indicating success or the type of error that occurred.
	/// Returns Connected if at least one amplifier was discovered, otherwise returns an error state.
	/// </returns>
	/// <remarks>
	/// This method:
	/// 1. Establishes the physical connection via the IPort
	/// 2. Queries the amplifier to determine how many units are present
	/// 3. Attaches property change listeners to all zones (enabling command execution)
	/// 4. Starts the background polling task to synchronize zone state
	/// 
	/// After a successful call, the Amplifier is ready for use. Zone properties
	/// can be read and written, and the polling task will keep state synchronized.
	/// </remarks>
	public async Task<ConnectionState> OpenAsync(CancellationToken token)
	{
		var state = await port.OpenAsync(token).ConfigureAwait(false);
		LogConnectionResult(state);

		if (state == ConnectionState.Connected)
		{
			AmplifierCount = await port.GetAmplifierCountAsync(token).ConfigureAwait(false);
			LogAmplifiersDetected(AmplifierCount);

			// Attach event handlers to all zones so property changes trigger command execution.
			// Guarded so that reconnecting (calling OpenAsync again after Close()) does not attach duplicate handlers.
			if (!_zoneHandlersAttached)
			{
				foreach (var zone in Zones)
				{
					zone.PropertyChanged += Zone_PropertyChanged;
				}
				_zoneHandlersAttached = true;
			}
		}

		// The timer and cancellation token source are single-use, so fresh instances are created on every
		// open attempt. This allows OpenAsync to be called again after Close() to reconnect.
		// The interval is stretched when the link is too slow to complete a full round of queries within the
		// preferred interval, which would otherwise queue polls faster than they can drain.
		_pollTimer.Dispose();
		_pollTimer = new PeriodicTimer(port.GetPollInterval(AmplifierCount, PreferredPollInterval));
		_pollTokenSource = new CancellationTokenSource();

		// Start the background polling task regardless of connection state
		// (it will handle the case where AmplifierCount is 0)
		_pollTask = PollAmplifiers(_pollTokenSource.Token);

		return state;
	}

	/// <summary>
	/// Asynchronously closes the connection to the amplifier and stops polling.
	/// </summary>
	/// <remarks>
	/// This method:
	/// 1. Signals the polling task to cancel
	/// 2. Waits for the polling task to complete
	/// 3. Closes the underlying port connection
	/// 
	/// After calling this method, the Amplifier should not be used.
	/// Call OpenAsync again to re-establish the connection.
	///
	/// Closing is a requested shutdown, so the cancellation it causes is not an error and is not surfaced to the
	/// caller. The poll task is also only assigned by <see cref="OpenAsync"/>, so closing an amplifier that was never
	/// opened must still release the port rather than throw.
	/// </remarks>
	public async Task Close()
	{
		await _pollTokenSource
			.CancelAsync()
			.ConfigureAwait(false);

		await AwaitPollTaskAsync().ConfigureAwait(false);

		await port
			.CloseAsync()
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Waits for the polling task to finish, treating the cancellation raised by shutting it down as completion.
	/// </summary>
	private async Task AwaitPollTaskAsync()
	{
		if (_pollTask is null)
		{
			return;
		}

		try
		{
			await _pollTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected: the loop is cancelled to stop it.
		}
	}

	/// <summary>
	/// Periodically polls all amplifiers to synchronize zone state with the hardware.
	/// </summary>
	/// <param name="token">A cancellation token used to stop the polling loop.</param>
	/// <remarks>
	/// This method runs in a background task and:
	/// 1. Queries each amplifier at 250ms intervals
	/// 2. For each amplifier, sends a query command requesting all zone statuses
	/// 3. Updates the corresponding HardwareZone objects with response data
	/// 4. Raises ZoneChanged events when zone state changes
	/// 
	/// If a query fails, an AmplifierStatusChanged event is raised but polling continues.
	/// The loop runs until cancellation is requested (typically via Close()).
	/// </remarks>
	private async Task PollAmplifiers(CancellationToken token)
	{
		while (await _pollTimer
			.WaitForNextTickAsync(token)
			.ConfigureAwait(false) &&
			!token.IsCancellationRequested)
		{
			// A single bad poll must never stop polling altogether, so every failure mode is contained per tick.
			try
			{
				var succeeded = await PollOnceAsync(token).ConfigureAwait(false);

				if (succeeded)
				{
					_consecutivePollFailures = 0;
					continue;
				}

				_consecutivePollFailures++;
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogPollIterationFailed(ex);
				RaiseStatus("Failed to poll the amplifier.");
				_consecutivePollFailures++;
			}

			// Repeated failures mean the link itself is gone rather than a single frame being lost, so the connection
			// is rebuilt. Without this the poll loop would keep running against a dead socket forever.
			if (_consecutivePollFailures >= PollFailuresBeforeReconnect)
			{
				await ReconnectAsync(token).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Consecutive failed poll rounds tolerated before the connection is considered lost.
	/// </summary>
	private const int PollFailuresBeforeReconnect = 4;

	private int _consecutivePollFailures;

	/// <summary>
	/// Tears down and re-establishes the port connection, then re-discovers the amplifier count.
	/// </summary>
	/// <remarks>
	/// Runs on the poll loop rather than as a separate watchdog so a reconnect can never overlap a poll. Zone event
	/// handlers are left attached because they are wired to zone objects that survive the reconnect.
	/// </remarks>
	private async Task ReconnectAsync(CancellationToken token)
	{
		LogAttemptingReconnect(_consecutivePollFailures);
		RaiseStatus("Connection lost, attempting to reconnect.");
		_consecutivePollFailures = 0;

		try
		{
			await port.CloseAsync().ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
		{
			// The link is already gone; closing it is best effort.
			LogReconnectCloseFailed(ex);
		}

		try
		{
			var state = await port.OpenAsync(token).ConfigureAwait(false);

			if (state != ConnectionState.Connected)
			{
				LogReconnectFailed(state);
				RaiseStatus("Failed to reconnect to the amplifier.");
				return;
			}

			AmplifierCount = await port.GetAmplifierCountAsync(token).ConfigureAwait(false);
			LogReconnected(AmplifierCount);
			RaiseStatus("Reconnected to the amplifier.");
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogReconnectError(ex);
			RaiseStatus("Failed to reconnect to the amplifier.");
		}
	}

	/// <summary>
	/// Performs one full poll round: queries every discovered amplifier, applies the responses and reconciles
	/// mirrored zones.
	/// </summary>
	/// <returns><see langword="true"/> if every amplifier answered; otherwise <see langword="false"/>.</returns>
	private async Task<bool> PollOnceAsync(CancellationToken token)
	{
		var allSucceeded = true;

		// Query each discovered amplifier
		for (int i = 1; i <= AmplifierCount; i++)
		{
			using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
			tokenSource.CancelAfter(port.CommandTimeout);
			var innerToken = tokenSource.Token;

			// Query this amplifier for all zone statuses
			var query = new QueryCommand((AmplifierIdentifier)i, ZoneIdentifier.All);

			CommandResult result;
			try
			{
				result = await port.ExecuteCommandAsync(query, innerToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
				// This amplifier timed out; the others may still answer.
				RaiseStatus("Failed to execute amplifier query due to timeout.");
				LogAmplifierQueryFailed((string)query);
				allSucceeded = false;
				continue;
			}

			if (result.Status == CommandResultStatus.Failed)
			{
				RaiseStatus("Failed to execute amplifier query.");
				LogAmplifierQueryFailed((string)query);
				allSucceeded = false;

				continue;
			}

			LogAmplifierPolled((string)query);

			// Process each zone response
			foreach (CommandResponse response in result.Response)
			{
				LogPollResponse(response);

				// Responses carry a per-amplifier zone number (1-6) while Zones is a flat array across all
				// amplifiers, so the amplifier address has to be folded in to find the right entry. Using the raw
				// zone number would write every amplifier's state onto amplifier one's zones.
				var zoneIndex = ((response.AmpId - 1) * ZonesPerAmplifier) + response.ZoneId - 1;

				if (zoneIndex < 0 || zoneIndex >= Zones.Length)
				{
					LogResponseZoneOutOfRange(response.AmpId, response.ZoneId);
					continue;
				}

				// Update the zone with the new state and detect if it changed
				var zoneChanged = Zones[zoneIndex].UpdateFromResponse(response);
				if (zoneChanged)
				{
					// Notify subscribers that this zone's state has changed
					ZoneChanged?.Invoke(this, new ZoneChangedEventArgs(response.AmpId, response.ZoneId));
				}
			}
		}

		// After refreshing all zones from the hardware, reconcile any primary/secondary
		// mirrored pairs that have drifted apart. Drift can happen in either direction: a
		// physical remote may have changed the primary (poll picks up the primary's new
		// value, which then needs pushing to its secondaries) or the secondary itself (poll
		// picks up the secondary's divergence, which needs correcting back to the primary).
		await ReconcileMirroredZonesAsync().ConfigureAwait(false);

		return allSucceeded;
	}

	/// <summary>
	/// Compares each configured primary/secondary zone pair across the mirrored properties and
	/// pushes corrections to the secondary whenever it has drifted from its primary.
	/// </summary>
	private async Task ReconcileMirroredZonesAsync()
	{
		foreach (var (primaryId, secondaryId) in ZoneLinks.GetAllLinks())
		{
			var primary = Zones[primaryId - 1];
			var secondary = Zones[secondaryId - 1];

			var corrected = false;

			foreach (var commandType in SetCommandType.Mirrored)
			{
				var primaryValue = primary.GetMirroredValue(commandType);
				var secondaryValue = secondary.GetMirroredValue(commandType);

				if (primaryValue == secondaryValue)
				{
					continue;
				}

				secondary.ApplyMirroredValue(commandType, primaryValue);
				await SendSetCommandAsync(secondary, commandType, primaryValue).ConfigureAwait(false);
				corrected = true;
			}

			if (corrected)
			{
				ZoneChanged?.Invoke(this, new ZoneChangedEventArgs((int)secondary.AmpId, (int)secondary.ZoneId));
			}
		}
	}

	/// <summary>
	/// Asynchronously releases resources used by the Amplifier.
	/// </summary>
	/// <remarks>
	/// This method:
	/// 1. Cancels the polling task
	/// 2. Waits for the polling task to complete (with exception handling for cancellation)
	/// 3. Disposes the underlying port connection
	/// 
	/// This is the preferred way to clean up an Amplifier instance.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		await _pollTokenSource.CancelAsync().ConfigureAwait(false);
		await AwaitPollTaskAsync().ConfigureAwait(false);

		DetachZoneHandlers();
		_pollTimer.Dispose();
		_pollTokenSource.Dispose();
		await port.DisposeAsync().ConfigureAwait(false);

		GC.SuppressFinalize(this);
	}

	private bool _disposed;

	/// <summary>
	/// Unsubscribes from every zone's property changed event so that disposing the amplifier does not leave zones
	/// holding a reference to it, and so a disposed amplifier cannot be driven into sending commands.
	/// </summary>
	private void DetachZoneHandlers()
	{
		if (!_zoneHandlersAttached)
		{
			return;
		}

		foreach (var zone in Zones)
		{
			zone.PropertyChanged -= Zone_PropertyChanged;
		}

		_zoneHandlersAttached = false;
	}

	/// <summary>
	/// Synchronously releases resources used by the Amplifier.
	/// </summary>
	/// <remarks>
	/// This method is provided for compatibility with the IDisposable interface.
	/// Prefer using DisposeAsync() and the using statement when possible.
	/// 
	/// This method:
	/// 1. Cancels the polling task
	/// 2. Synchronously waits for the polling task to complete (with exception handling)
	/// 3. Disposes the underlying port connection
	/// </remarks>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_pollTokenSource.Cancel();
		try
		{
			_pollTask?.Wait();
		}
		catch (Exception ex) when (ex is OperationCanceledException or AggregateException)
		{
			// Task.Wait wraps cancellation in an AggregateException, so both forms are expected here.
		}

		DetachZoneHandlers();
		_pollTimer.Dispose();
		_pollTokenSource.Dispose();
		port.Dispose();

		GC.SuppressFinalize(this);
	}
}
