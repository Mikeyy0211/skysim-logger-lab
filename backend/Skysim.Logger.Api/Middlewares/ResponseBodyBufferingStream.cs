namespace Skysim.Logger.Api.Middlewares;

public class ResponseBodyBufferingStream : Stream
{
    private readonly Stream _innerStream;
    private readonly MemoryStream _buffer;

    public ResponseBodyBufferingStream(Stream innerStream)
    {
        _innerStream = innerStream;
        _buffer = new MemoryStream();
    }

    public byte[] GetBuffer() => _buffer.ToArray();

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        _innerStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        _innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
        _innerStream.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
        await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
