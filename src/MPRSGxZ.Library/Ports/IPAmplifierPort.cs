using Microsoft.Extensions.Logging;
using MPRSGxZ.Library.Commands;
using MPRSGxZ.Library.Enumerators;
using MPRSGxZ.Library.Providers;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MPRSGxZ.Library.Ports;

/// <summary>
/// Implements RS-232 serial communication with Marantz amplifiers over TCP/IP.
/// Handles baud rate negotiation, command transmission, and response parsing.
/// Includes resilience features with automatic retry on transient failures.
/// </summary>
public partial class IPAmplifierPort(ILogger<IPAmplifierPort> logger, IEndpointProvider endpointProvider) : IPort
{
	private TcpClient _client = new();

	/// <summary>
	/// The stream for the current connection, or <see langword="null"/> when there is no established connection.
	/// </summary>
	/// <remarks>
	/// This is genuinely null before the first successful connect, after a connect that failed, and after closing, so
	/// it is declared nullable to keep those states visible to the compiler rather than suppressed. Everything that
	/// transmits reaches <see cref="RequireStream"/> first, which converts the null into a meaningful failure instead
	/// of a <see cref="NullReferenceException"/>.
	/// </remarks>
	private NetworkStream? _stream;
	private readonly SemaphoreSlim _semaphore = new(1);
	private bool _disposed;

	private static readonly string[] _separator = ["#", ">", "\r", "\n", "\0"];

	/// <summary>
	/// The character the amplifier prints when it is ready for the next command.
	/// </summary>
	private const byte Prompt = (byte)'#';

	/// <summary>
	/// Retries the write and echo exchange. Handles both a mismatched echo (returned as <see langword="false"/>) and
	/// the transient socket faults that a serial to Ethernet bridge produces when it drops or re-establishes its
	/// serial side, which would otherwise surface as a hard failure on the first blip. Backoff is exponential so a
	/// bridge that needs a moment to settle is given progressively more time.
	/// </summary>
	private readonly AsyncRetryPolicy<bool> _writeRetryPolicy = Policy.HandleResult<bool>(r => r == false)
		.Or<IOException>()
		.Or<SocketException>()
		.WaitAndRetryAsync(
			retryCount: 3,
			sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(50 * Math.Pow(2, retryAttempt - 1)),
			onRetryAsync: (outcome, timespan, retryCount, context) =>
			{
				LogCommandRetryAttempt(logger, retryCount);
				return Task.CompletedTask;
			});

	public ConnectionState ConnectionState { get; private set; }

	/// <summary>
	/// The baud rate the link is currently operating at. Defaults to the slowest supported rate so that timeouts are
	/// generous until negotiation has completed.
	/// </summary>
	private Baud _negotiatedBaud = Enum.GetValues<Baud>()[0];

	public TimeSpan CommandTimeout => BaudTiming.GetCommandTimeout(_negotiatedBaud);

	public TimeSpan GetPollInterval(int amplifierCount, TimeSpan preferredInterval)
		=> BaudTiming.GetPollInterval(_negotiatedBaud, amplifierCount, preferredInterval);

	public async Task<ConnectionState> OpenAsync(CancellationToken cancellationToken)
	{
		try
		{
			// A TcpClient cannot be reconnected once closed/disposed, so a fresh instance is created on every
			// open attempt. This allows this port to be reused for reconnects (e.g. after the endpoint changes)
			// instead of requiring a brand new IPAmplifierPort instance. The previous one is disposed first,
			// because repeatedly retrying an unreachable endpoint would otherwise abandon a socket per attempt.
			_client.Dispose();
			_stream = null;
			_client = new TcpClient();
			LogConnectingToEndpoint(endpointProvider.Endpoint);
			await _client.ConnectAsync(endpointProvider.Endpoint, cancellationToken);
			_stream = _client.GetStream();
		}
		catch (Exception ex)
		{
			LogConnectFailed(ex, endpointProvider.Endpoint);
			return ConnectionState = ConnectionState.ErrorInvalidAddress;
		}

		var baudRates = Enum.GetValues<Baud>();
		var maxBaud = baudRates[^1];
		var minBaud = baudRates[0];

		// Step 1: Attempt connection at maximum baud rate
		LogAttemptingMaxBaudRate((int)maxBaud);
		await ApplyBaudAsync(maxBaud, cancellationToken).ConfigureAwait(false);

		if (await ValidateBaudAsync(cancellationToken).ConfigureAwait(false))
		{
			LogCommunicationEstablished((int)maxBaud);
			return ConnectionState = ConnectionState.Connected;
		}

		LogBaudRateValidationFailed((int)maxBaud);

		// Step 2: Attempt connection at minimum baud rate
		LogAttemptingMinBaudRate((int)minBaud);
		await ApplyBaudAsync(minBaud, cancellationToken).ConfigureAwait(false);

		if (!await ValidateBaudAsync(cancellationToken).ConfigureAwait(false))
		{
			LogNoBaudRateNegotiated();
			return ConnectionState = ConnectionState.ErrorCommunication;
		}

		LogCommunicationEstablished((int)minBaud);

		// Step 3: Try to reconfigure for maximum baud rate
		LogAttemptingBaudRateUpgrade((int)minBaud, (int)maxBaud);
		try
		{
			await ExecuteCommandAsync(new BaudCommand(maxBaud), cancellationToken).ConfigureAwait(false);
			await ApplyBaudAsync(maxBaud, cancellationToken).ConfigureAwait(false);

			if (await ValidateBaudAsync(cancellationToken).ConfigureAwait(false))
			{
				LogBaudRateUpgradeSuccessful((int)maxBaud);
				return ConnectionState = ConnectionState.Connected;
			}
		}
		catch (Exception ex)
		{
			LogBaudRateUpgradeFailed(ex, (int)maxBaud);
		}

		// Step 4: Fall back to minimum baud rate
		LogFallingBackToMinBaudRate((int)minBaud);
		await ApplyBaudAsync(minBaud, cancellationToken).ConfigureAwait(false);

		return ConnectionState = ConnectionState.Connected;
	}

	/// <summary>
	/// Reconfigures the serial server for the given baud rate, records it as the negotiated rate and empties anything
	/// the rate change left in the stream.
	/// </summary>
	/// <remarks>
	/// The amplifier switches rate as soon as it acknowledges a <see cref="BaudCommand"/>, but the serial server only
	/// switches once its option bytes arrive. Everything transmitted between those two moments is decoded at the wrong
	/// rate and appears as line noise, so the stream is drained after a short settle delay before any validation is
	/// attempted.
	/// </remarks>
	private async Task ApplyBaudAsync(Baud baud, CancellationToken cancellationToken)
	{
		await WriteOptionBytesAsync(SerialServerOptions.GetOptionBytes(baud), cancellationToken).ConfigureAwait(false);
		_negotiatedBaud = baud;

		await Task.Delay(BaudSettleDelay, cancellationToken).ConfigureAwait(false);
		await DrainAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Time allowed for both ends of the link to finish switching baud rate before the stream is drained.
	/// </summary>
	private static readonly TimeSpan BaudSettleDelay = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Number of consecutive successful probes required to accept a baud rate.
	/// </summary>
	private const int BaudProbeAttempts = 2;

	/// <summary>
	/// Verifies that the link is actually usable at the current baud rate.
	/// </summary>
	/// <remarks>
	/// Merely observing that bytes arrived is not sufficient: at a mismatched rate the amplifier's reply is still
	/// received, just decoded as garbage, so any presence-only check reports success for every rate. Instead this
	/// issues a real zone query and requires a well formed status line back, which can only be produced when both ends
	/// agree on the rate. The probe is repeated so a single lucky frame cannot pass a marginal link.
	/// </remarks>
	protected async Task<bool> ValidateBaudAsync(CancellationToken cancellationToken)
	{
		for (var attempt = 1; attempt <= BaudProbeAttempts; attempt++)
		{
			if (!await ProbeAmplifierAsync(AmplifierIdentifier.One, cancellationToken).ConfigureAwait(false))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Queries an amplifier for all of its zones and reports whether it answered with a complete, well formed status
	/// block.
	/// </summary>
	/// <remarks>
	/// This is the shared primitive behind both baud validation and amplifier discovery, because the two ask the same
	/// question of the link: did a real amplifier answer this query intelligibly? An amplifier that is not present
	/// returns nothing and simply re-prints the prompt, and a link at the wrong baud rate returns bytes that fail to
	/// parse; both are rejected the same way.
	/// </remarks>
	private async Task<bool> ProbeAmplifierAsync(AmplifierIdentifier ampId, CancellationToken cancellationToken)
	{
		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await ProbeAmplifierCoreAsync(ampId, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Probe implementation. The caller must already hold <see cref="_semaphore"/>.
	/// </summary>
	private async Task<bool> ProbeAmplifierCoreAsync(AmplifierIdentifier ampId, CancellationToken cancellationToken)
	{
		var query = new QueryCommand(ampId, ZoneIdentifier.All);

		try
		{
			// A single attempt only: a probe is a question, not a command that must land, and burning the retry budget
			// here would make an unusable baud rate look merely slow.
			if (!await WriteCommandAsync(query, cancellationToken, singleAttempt: true).ConfigureAwait(false))
			{
				return false;
			}

			var raw = await ReadUntilPromptAsync(ExpectedResponseBytes(query), cancellationToken).ConfigureAwait(false);

			var text = Encoding.ASCII.GetString(raw);
			var lines = text.Split(_separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			// A present amplifier answers with one parseable line per zone, and always six lines even on a four zone
			// model. Requiring every line to parse rejects both a partial read and a rate mismatch.
			if (lines.Length == query.ExpectedLines && lines.All(line => CommandResponse.TryParse(line, out _)))
			{
				return true;
			}

			// Logged because this is the failure that decides baud negotiation and amplifier discovery, and without the
			// bytes there is no way to tell an absent amplifier from a rate mismatch or a changed frame format.
			LogProbeRejected((int)ampId, lines.Length, query.ExpectedLines, Convert.ToBase64String(raw));
			return false;
		}
		catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
		{
			LogProbeFailed(ex, (int)ampId);
			return false;
		}
	}

	/// <summary>
	/// Returns the stream for the current connection, or throws if there is no established connection.
	/// </summary>
	/// <remarks>
	/// Transmitting without a connection is reported as an <see cref="IOException"/> because that is how callers
	/// already classify a link that is unusable: commands surface it as a failure and the poll loop counts it towards
	/// a reconnect. A <see cref="NullReferenceException"/> escaping instead would bypass those handlers, which match
	/// on specific exception types, and surface as an unhandled fault.
	/// </remarks>
	/// <exception cref="IOException">There is no established connection.</exception>
	private NetworkStream RequireStream()
		=> _stream ?? throw new IOException("The port is not connected.");

	internal async Task WriteOptionBytesAsync(byte[] option, CancellationToken token)
	{
		await _semaphore.WaitAsync(token).ConfigureAwait(false);

		try
		{
			LogTransmittingOptionBytes(Convert.ToBase64String(option));
			await RequireStream().WriteAsync(option, token).ConfigureAwait(false);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Closes the connection, leaving the port able to be opened again.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The stream is only assigned once a connection has actually been established, so it is null whenever closing
	/// follows an <see cref="OpenAsync"/> that failed to connect, or an open that was never attempted. Closing has to
	/// tolerate that: a caller reconnecting after a bad endpoint reaches this before it reaches a working connection,
	/// and throwing there would leave the port permanently unusable.
	/// </para>
	/// <para>
	/// The reference is cleared afterwards so that a second close, or a close following a failed reopen, does not
	/// operate on the stream of a connection that is already gone.
	/// </para>
	/// </remarks>
	public async Task CloseAsync()
	{
		var stream = _stream;
		_stream = null;

		if (stream is not null)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
		}

		_client.Close();
		ConnectionState = ConnectionState.Disconnected;
	}

	/// <summary>
	/// Sends the specified command asynchronously and returns the result, including any responses received from the target
	/// device.
	/// </summary>
	/// <remarks>This method applies a retry policy to the command transmission. If the command fails to transmit,
	/// the returned CommandResult will have a status of Failed and an empty response collection. The method is thread-safe
	/// and ensures exclusive access to the underlying stream during command transmission and response retrieval.
	///
	/// TODO: the semaphore is strictly first-come-first-served, so a user initiated command queues behind any in flight
	/// poll and can have its own timeout expire before it ever reaches the wire. A priority queue, or splitting polls
	/// into interruptible units, would let interactive commands pre-empt background polling.</remarks>
	/// <param name="command">The command to transmit.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the command transmission and response retrieval.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a CommandResult indicating the status
	/// of the transmission and any responses received.</returns>
	public async Task<CommandResult> ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken)
	{
		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			LogTransmittingCommand(command.ToString());
			var result = await WriteCommandAsync(command, cancellationToken).ConfigureAwait(false);

			if (!result)
			{
				LogCommandTransmissionFailed(command.ToString());
				return new CommandResult(CommandResultStatus.Failed, []);
			}

			var raw = await ReadUntilPromptAsync(ExpectedResponseBytes(command), cancellationToken).ConfigureAwait(false);

			var lines = Encoding.ASCII.GetString(raw)
				.Split(_separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			var responses = new List<CommandResponse>(lines.Length);
			var malformed = 0;

			foreach (var line in lines)
			{
				if (CommandResponse.TryParse(line, out var response))
				{
					responses.Add(response);
				}
				else
				{
					malformed++;
				}
			}

			// A corrupt frame is a transient condition on a serial link, so it is reported as a failed command rather
			// than thrown. Throwing here would propagate out of the caller's poll loop and stop polling permanently.
			if (malformed > 0)
			{
				LogMalformedResponse(command.ToString(), malformed, Encoding.ASCII.GetString(raw));

				// A short response that otherwise parsed cleanly still means the exchange did not complete.
				await DrainCoreAsync(cancellationToken).ConfigureAwait(false);
				return new CommandResult(CommandResultStatus.Failed, []);
			}

			if (responses.Count != command.ExpectedLines)
			{
				LogUnexpectedResponseCount(command.ToString(), command.ExpectedLines, responses.Count);
				await DrainCoreAsync(cancellationToken).ConfigureAwait(false);
				return new CommandResult(CommandResultStatus.Failed, []);
			}

			return new CommandResult(CommandResultStatus.Success, responses);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Gets the maximum number of bytes a command's response can occupy.
	/// </summary>
	/// <remarks>
	/// This is an upper bound for the read, not an exact length: the read stops at the prompt. Derived from the payload
	/// width plus the framing each line carries, so a change to the field list cannot silently truncate reads.
	/// </remarks>
	private static int ExpectedResponseBytes(ICommand command)
		=> (command.ExpectedLines * (CommandResponse.ResponseLength + LineFramingBytes)) + PromptBytes;

	/// <summary>
	/// The number of framing bytes that accompany each status line: a <c>#&gt;</c> prefix and a <c>\r\r\n</c>
	/// terminator, as observed on real hardware.
	/// </summary>
	private const int LineFramingBytes = 5;

	/// <summary>
	/// The number of bytes in the trailing prompt that terminates a response block.
	/// </summary>
	private const int PromptBytes = 1;

	public async Task<int> GetAmplifierCountAsync(CancellationToken cancellationToken)
	{
		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// A maximum of three amplifiers can be chained. Probing from the highest address down means the first
			// amplifier that answers is also the total count, since addresses are assigned contiguously.
			for (int i = 3; i >= 1; i--)
			{
				LogDeterminingAmplifierCount(i);

				if (await ProbeAmplifierCoreAsync((AmplifierIdentifier)i, cancellationToken).ConfigureAwait(false))
				{
					LogAmplifierCountDetermined(i);
					return i;
				}

				// An absent amplifier leaves the re-printed prompt behind; clear it so the next probe starts clean.
				await DrainCoreAsync(cancellationToken).ConfigureAwait(false);
			}

			LogFailedToDetermineAmplifierCount();
			throw new InvalidDataException("Failed to determine the amplifier count. No suitable response was detected.");
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Sends the specified command to the underlying stream and verifies that the echoed response matches the command
	/// sent.
	/// </summary>
	/// <remarks>This method writes the command to the stream with CRLF termination and reads back the echoed
	/// response for validation. If the echo does not match the command, the method returns false. The operation is
	/// performed asynchronously and may be canceled via the provided token.</remarks>
	/// <param name="command">The command to transmit to the stream. Must not be null.</param>
	/// <param name="token">A cancellation token that can be used to cancel the asynchronous operation.</param>
	/// <returns>true if the echoed response matches the command sent; otherwise, false.</returns>
	/// <param name="singleAttempt">
	/// When <see langword="true"/>, the retry policy is bypassed. Used by probes, where a failure is a meaningful
	/// answer rather than something to retry.
	/// </param>
	private async Task<bool> WriteCommandAsync(ICommand command, CancellationToken token, bool singleAttempt = false)
	{
		// Prepare the command with CRLF termination
		var writeBuffer = Encoding.ASCII.GetBytes($"{command}\r\n");
		LogTransmittingBytes(Convert.ToBase64String(writeBuffer));

		// Checked up front so that having no connection at all fails immediately. The retry policy treats an
		// IOException as transient, which is right for a link that faults mid-command but pointless when there is
		// nothing to write to, and it would spend the caller's timeout on attempts that cannot reach the wire.
		var stream = RequireStream();

		return singleAttempt
			? await WriteAndVerifyEchoAsync(stream, writeBuffer, token).ConfigureAwait(false)
			: await _writeRetryPolicy
				.ExecuteAsync(innerToken => WriteAndVerifyEchoAsync(stream, writeBuffer, innerToken), token)
				.ConfigureAwait(false);
	}

	/// <summary>
	/// Transmits a command and validates the echo the amplifier sends back.
	/// </summary>
	/// <remarks>
	/// The echo is read here so it cannot be mistaken for part of the response, and comparing it against what was sent
	/// doubles as a check that both ends of the link agree on the baud rate.
	///
	/// The echo read is bounded by its own timeout rather than relying on the caller's. A link at a rate the amplifier
	/// is not using produces no echo at all, and an unbounded read would then consume the caller's entire budget on the
	/// first rate tried, leaving nothing for the remaining rates. Treating the absent echo as a fast, local failure is
	/// what allows baud negotiation to fall back within a single caller timeout.
	/// </remarks>
	/// <param name="stream">
	/// The stream to transmit on. Passed in rather than read from the field so that every attempt made by the retry
	/// policy uses the connection the command started on, instead of silently continuing on a replacement.
	/// </param>
	/// <param name="writeBuffer">The bytes to transmit.</param>
	/// <param name="token">Cancels the exchange.</param>
	private async Task<bool> WriteAndVerifyEchoAsync(NetworkStream stream, byte[] writeBuffer, CancellationToken token)
	{
		var echoBuffer = new byte[writeBuffer.Length];

		await stream.WriteAsync(writeBuffer, token).ConfigureAwait(false);

		using var echoTimeout = new CancellationTokenSource(CommandTimeout);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, echoTimeout.Token);

		try
		{
			await stream.ReadExactlyAsync(echoBuffer, linked.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (echoTimeout.IsCancellationRequested && !token.IsCancellationRequested)
		{
			LogEchoTimeout(Convert.ToBase64String(writeBuffer), CommandTimeout.TotalMilliseconds);
			return false;
		}

		if (!writeBuffer.SequenceEqual(echoBuffer))
		{
			LogEchoMismatch(Convert.ToBase64String(writeBuffer), Convert.ToBase64String(echoBuffer));
			await DrainCoreAsync(token).ConfigureAwait(false);
			return false;
		}

		return true;
	}

	/// <summary>
	/// Reads from the stream until the amplifier prints its prompt, the byte budget is exhausted, the peer closes, or
	/// the link falls idle.
	/// </summary>
	/// <remarks>
	/// The prompt is the only reliable end of message marker: response length varies with the command, and an absent
	/// amplifier answers a query with nothing but a re-printed prompt. Reading to the prompt rather than to a computed
	/// byte count means a short or missing response ends the read promptly instead of blocking until the caller's
	/// timeout, and a longer than expected response cannot overrun the buffer.
	/// </remarks>
	/// <param name="maxBytes">Upper bound on the number of bytes to read.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The bytes read, excluding nothing; the prompt is included when one was received.</returns>
	private async Task<byte[]> ReadUntilPromptAsync(int maxBytes, CancellationToken cancellationToken)
	{
		var buffer = new byte[Math.Max(maxBytes, 1)];
		var total = 0;
		var stream = RequireStream();

		// A gap longer than the idle timeout means the amplifier has stopped talking, whether or not it sent a prompt.
		// Without this an absent or wedged amplifier would hold the read open until the caller's own timeout fires.
		// The first byte gets the full command budget, because it has to cover the amplifier's processing time as well
		// as line time; subsequent gaps only need to cover line time and so use the shorter idle timeout.
		using var idleTimeout = new CancellationTokenSource(CommandTimeout);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleTimeout.Token);

		try
		{
			while (total < buffer.Length)
			{
				var read = await stream
					.ReadAsync(buffer.AsMemory(total, buffer.Length - total), linked.Token)
					.ConfigureAwait(false);

				// Zero bytes from a NetworkStream means the peer closed the connection.
				if (read == 0)
				{
					break;
				}

				var previous = total;
				total += read;

				if (Array.IndexOf(buffer, Prompt, previous, read) >= 0)
				{
					break;
				}

				idleTimeout.CancelAfter(IdleTimeout);
			}
		}
		catch (OperationCanceledException) when (idleTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// The link fell idle. Whatever arrived is returned so the caller can judge whether it is usable.
			LogReadIdleTimeout(total);
		}

		return buffer[..total];
	}

	/// <summary>
	/// How long the link may stay silent mid-read before the read is considered complete.
	/// </summary>
	private static readonly TimeSpan IdleTimeout = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// How long a drain waits for the link to fall silent.
	/// </summary>
	private static readonly TimeSpan DrainQuietPeriod = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Hard ceiling on how long a drain may run, so a chattering link cannot stall the caller indefinitely.
	/// </summary>
	private static readonly TimeSpan DrainMaxDuration = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Discards everything currently pending in the stream, restoring a known good state after an echo mismatch,
	/// a baud rate change, or a malformed response.
	/// </summary>
	/// <returns>The number of bytes discarded.</returns>
	private async Task<int> DrainAsync(CancellationToken cancellationToken)
	{
		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await DrainCoreAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Drain implementation. The caller must already hold <see cref="_semaphore"/>.
	/// </summary>
	/// <remarks>
	/// Unlike the obsolete <see cref="ClearStreamAsync"/> this writes nothing: sending a newline to provoke a response
	/// adds to the very stream being emptied. It also keeps reading until a quiet period elapses rather than issuing a
	/// single read, because one read only removes whatever happened to have arrived so far and leaves the remainder to
	/// be misread as the next command's response.
	/// </remarks>
	private async Task<int> DrainCoreAsync(CancellationToken cancellationToken)
	{
		var buffer = new byte[1024];
		var discarded = 0;
		var deadline = DateTime.UtcNow + DrainMaxDuration;
		var stream = RequireStream();

		while (DateTime.UtcNow < deadline)
		{
			if (!stream.DataAvailable)
			{
				// Give a slow talker one quiet period to produce more before declaring the stream empty.
				await Task.Delay(DrainQuietPeriod, cancellationToken).ConfigureAwait(false);

				if (!stream.DataAvailable)
				{
					break;
				}
			}

			var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

			if (read == 0)
			{
				break;
			}

			discarded += read;
		}

		if (discarded > 0)
		{
			LogStreamDrained(discarded);
		}

		return discarded;
	}

	[Obsolete($"Writes to the stream it is draining and only performs a single read, so it can leave data behind. Use the internal drain instead; retained for compatibility with derived types.")]
	protected async Task<string> ClearStreamAsync(CancellationToken cancellationToken)
	{
		LogClearingStream();
		byte[] buffer = new byte[1024];
		var stream = RequireStream();
		await stream.WriteAsync("\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
		var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

		return Encoding.ASCII.GetString(buffer, 0, read);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_stream is not null)
		{
			await _stream.DisposeAsync().ConfigureAwait(false);
		}

		_client.Dispose();
		_semaphore.Dispose();
		ConnectionState = ConnectionState.Disconnected;

		GC.SuppressFinalize(this);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_stream?.Dispose();
		_client.Dispose();
		_semaphore.Dispose();
		ConnectionState = ConnectionState.Disconnected;

		GC.SuppressFinalize(this);
	}
}
