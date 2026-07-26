using System.Text;
using System.Threading.Channels;
using Ft.Core.Pipeline;

namespace Ft.Core.Logging;

/// <summary>
/// Streams raw traffic to a log file: one line per chunk with ISO timestamp,
/// direction and hex bytes. Writes go through an unbounded channel + writer
/// task so the RX path never blocks on disk I/O. Logging taps the pipeline's
/// pre-framing byte stream, so the display ring limit does not truncate logs.
/// </summary>
public sealed class RawLogWriter : IAsyncDisposable
{
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
    private readonly Task _writerTask;
    private readonly string _path;

    public string Path => _path;

    public RawLogWriter(string path)
    {
        _path = path;
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public void WriteChunk(ReadOnlySpan<byte> data, FrameDirection dir, DateTimeOffset ts)
    {
        var line = new StringBuilder(40 + data.Length * 3);
        line.Append(ts.ToString("yyyy-MM-dd'T'HH:mm:ss.fff"));
        line.Append(dir == FrameDirection.Rx ? " RX " : " TX ");
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0) line.Append(' ');
            line.Append(data[i].ToString("X2"));
        }
        _lines.Writer.TryWrite(line.ToString());
    }

    private async Task WriteLoopAsync()
    {
        await using var stream = new StreamWriter(_path, append: true, Encoding.UTF8);
        await foreach (var line in _lines.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await stream.WriteLineAsync(line).ConfigureAwait(false);
        }
        await stream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Complete the channel and wait until everything is on disk.</summary>
    public async Task StopAsync()
    {
        _lines.Writer.TryComplete();
        await _writerTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
