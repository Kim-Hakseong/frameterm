namespace Ft.Core.Transport;

/// <summary>
/// Byte-stream transport abstraction (serial today, TCP later). All methods
/// are async and cancellation-aware; failures come back as Result values so
/// the receive loop never dies on I/O errors.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    string Description { get; }
    bool IsOpen { get; }

    Task<Result<bool>> OpenAsync(CancellationToken ct);
    Task CloseAsync();

    /// <summary>Write all bytes; returns count written.</summary>
    Task<Result<int>> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>
    /// Read available bytes into the buffer; returns 0 when the transport
    /// closed, an error Result on I/O failure.
    /// </summary>
    Task<Result<int>> ReadAsync(Memory<byte> buffer, CancellationToken ct);
}
