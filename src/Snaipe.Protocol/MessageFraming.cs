using System.Buffers;
using System.Text.Json;

namespace Snaipe.Protocol;

/// <summary>
/// Length-prefixed JSON message framing over a <see cref="Stream"/>.
/// Format: [4 bytes: little-endian int32 payload length][UTF-8 JSON payload]
/// </summary>
public static class MessageFraming
{
    /// <summary>Maximum payload size: 50 MB.</summary>
    public const int MaxPayloadSize = 50 * 1024 * 1024;

    /// <summary>Protocol version string.</summary>
    public const string ProtocolVersion = "1.0";

    /// <summary>
    /// Read a single length-prefixed message from the stream.
    /// Returns null if the stream is closed (0 bytes read for the length prefix).
    /// </summary>
    public static async Task<InspectorMessage?> ReadMessageAsync(
        Stream stream, CancellationToken ct = default)
    {
        // Read 4-byte length prefix
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, ct).ConfigureAwait(false);
        if (bytesRead == 0)
            return null; // stream closed

        if (bytesRead < 4)
            throw new IOException("Unexpected end of stream while reading message length prefix.");

        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

        if (payloadLength <= 0)
            throw new IOException($"Invalid payload length: {payloadLength}");

        if (payloadLength > MaxPayloadSize)
            throw new InvalidOperationException(
                $"Payload size {payloadLength} exceeds maximum of {MaxPayloadSize} bytes.");

        // Read payload
        var payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadLength);
        try
        {
            bytesRead = await ReadExactAsync(stream, payloadBuffer.AsMemory(0, payloadLength), ct)
                .ConfigureAwait(false);

            if (bytesRead < payloadLength)
                throw new IOException("Unexpected end of stream while reading message payload.");

            var message = JsonSerializer.Deserialize(
                payloadBuffer.AsSpan(0, payloadLength),
                ProtocolJsonContext.Default.InspectorMessage);

            return message ?? throw new JsonException("Deserialized message was null.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payloadBuffer);
        }
    }

    /// <summary>
    /// Write a single length-prefixed message to the stream.
    /// </summary>
    public static async Task WriteMessageAsync(
        Stream stream, InspectorMessage message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            message, ProtocolJsonContext.Default.InspectorMessage);

        if (payload.Length > MaxPayloadSize)
            throw new InvalidOperationException(
                $"Serialized payload size {payload.Length} exceeds maximum of {MaxPayloadSize} bytes.");

        var lengthPrefix = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Read exactly <paramref name="buffer"/>.Length bytes from the stream.
    /// Returns 0 if the stream is at EOF on the very first read.
    /// </summary>
    private static async Task<int> ReadExactAsync(
        Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct).ConfigureAwait(false);
            if (read == 0)
                return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}
