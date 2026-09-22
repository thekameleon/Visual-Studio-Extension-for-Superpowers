using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TheKameleon.Superpowers.Bridge.Contracts;

/// <summary>
/// Shared length-prefixed JSON framing used by both ends of the named-pipe bridge transport.
/// Public .NET APIs only (System.IO.Pipes stream + System.Text.Json) — no VS-private surface.
/// </summary>
public static class BridgeFraming
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        var lengthPrefix = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T?> ReadMessageAsync<T>(Stream stream, CancellationToken cancellationToken)
        where T : class
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var lengthPrefix = new byte[4];
        if (!await ReadExactAsync(stream, lengthPrefix, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = BitConverter.ToInt32(lengthPrefix, 0);
        if (length < 0 || length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("Bridge message length prefix out of bounds; refusing to allocate.");
        }

        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
