using System.IO.Pipes;
using Snaipe.Protocol;

namespace Snaipe.Inspector;

/// <summary>
/// Named pipe IPC client that connects to a Snaipe agent and sends/receives messages.
/// </summary>
public sealed class InspectorIpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public bool IsConnected => _pipe is { IsConnected: true };

    /// <summary>
    /// Connect to an agent's named pipe.
    /// </summary>
    public async Task ConnectAsync(string pipeName, CancellationToken ct = default)
    {
        _pipe?.Dispose();

        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(ConnectionTimeout);

        await _pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request and wait for a typed response.
    /// </summary>
    public async Task<TResponse> SendAsync<TResponse>(InspectorMessage request, CancellationToken ct = default)
        where TResponse : InspectorMessage
    {
        if (_pipe is not { IsConnected: true })
            throw new InvalidOperationException("Not connected to an agent.");

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(RequestTimeout);

        await MessageFraming.WriteMessageAsync(_pipe, request, requestCts.Token).ConfigureAwait(false);
        var response = await MessageFraming.ReadMessageAsync(_pipe, requestCts.Token).ConfigureAwait(false);

        if (response is null)
            throw new IOException("Connection closed while waiting for response.");

        if (response is TResponse typed)
            return typed;

        if (response is ErrorResponse error)
            throw new SnaipeProtocolException(error.ErrorCode, error.Error, error.Details);

        throw new InvalidOperationException(
            $"Unexpected response type: {response.GetType().Name}, expected {typeof(TResponse).Name}");
    }

    /// <summary>
    /// Send a request and return the raw InspectorMessage response (either the expected type or ErrorResponse).
    /// </summary>
    public async Task<InspectorMessage> SendRawAsync(InspectorMessage request, CancellationToken ct = default)
    {
        if (_pipe is not { IsConnected: true })
            throw new InvalidOperationException("Not connected to an agent.");

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        requestCts.CancelAfter(RequestTimeout);

        await MessageFraming.WriteMessageAsync(_pipe, request, requestCts.Token).ConfigureAwait(false);
        var response = await MessageFraming.ReadMessageAsync(_pipe, requestCts.Token).ConfigureAwait(false);

        return response ?? throw new IOException("Connection closed while waiting for response.");
    }

    public void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}

/// <summary>
/// Exception thrown when the agent returns an ErrorResponse.
/// </summary>
public class SnaipeProtocolException : Exception
{
    public int ErrorCode { get; }
    public string? Details { get; }

    public SnaipeProtocolException(int errorCode, string error, string? details)
        : base($"[{errorCode}] {error}" + (details is not null ? $": {details}" : ""))
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
