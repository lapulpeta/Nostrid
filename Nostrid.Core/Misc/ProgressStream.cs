namespace Nostrid.Misc
{
    public class ProgressStream : Stream
    {
        private Stream m_input;

        public event EventHandler<float>? UpdateProgress;

        public ProgressStream(Stream input)
        {
            m_input = input;
        }
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_input.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int n = await m_input.ReadAsync(buffer, offset, count, cancellationToken);
            UpdateProgress?.Invoke(this, (1.0f * Position) / Length);
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int n = await m_input.ReadAsync(buffer, cancellationToken);
            UpdateProgress?.Invoke(this, (1.0f * Position) / Length);
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = m_input.Read(buffer, offset, count);
            UpdateProgress?.Invoke(this, (1.0f * Position) / Length);
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => m_input.Length;
        public override long Position
        {
            get { return m_input.Position; }
            set { m_input.Position = value; }
        }
    }
}
