using System.IO.Pipes;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Named pipe IPC server that accepts a single client connection at a time
/// and dispatches incoming messages to a handler.
/// </summary>
public sealed class AgentIpcServer : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipe;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public AgentIpcServer(string pipeName)
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Run the IPC server loop. Accepts one client at a time, reads messages,
    /// dispatches to handler, and writes responses.
    /// </summary>
    public async Task RunAsync(
        Func<InspectorMessage, CancellationToken, Task<InspectorMessage>> handler,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                System.Diagnostics.Debug.WriteLine($"[Snaipe.Agent] Waiting for connection on pipe '{_pipeName}'...");
                await _pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine("[Snaipe.Agent] Client connected.");

                await HandleClientAsync(handler, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Snaipe.Agent] Client disconnected: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Snaipe.Agent] Error: {ex}");
            }
            finally
            {
                DisposePipe();
            }

            // Brief delay before re-listening to prevent spin-looping.
            if (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(1000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task HandleClientAsync(
        Func<InspectorMessage, CancellationToken, Task<InspectorMessage>> handler,
        CancellationToken ct)
    {
        while (_pipe is { IsConnected: true } && !ct.IsCancellationRequested)
        {
            var request = await MessageFraming.ReadMessageAsync(_pipe, ct).ConfigureAwait(false);
            if (request is null)
            {
                System.Diagnostics.Debug.WriteLine("[Snaipe.Agent] Client closed connection.");
                break;
            }

            System.Diagnostics.Debug.WriteLine($"[Snaipe.Agent] Received: {request.GetType().Name} ({request.MessageId})");

            InspectorMessage response;
            try
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                requestCts.CancelAfter(RequestTimeout);

                response = await handler(request, requestCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                response = new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Request timed out",
                    Details = $"Handler exceeded {RequestTimeout.TotalSeconds}s timeout."
                };
            }
            catch (Exception ex)
            {
                response = new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Internal error",
                    Details = ex.Message
                };
            }

            await MessageFraming.WriteMessageAsync(_pipe, response, ct).ConfigureAwait(false);
        }
    }

    private void DisposePipe()
    {
        try
        {
            _pipe?.Dispose();
        }
        catch
        {
            // Best-effort cleanup.
        }
        _pipe = null;
    }

    public void Dispose()
    {
        DisposePipe();
    }
}
