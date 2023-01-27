namespace Nostrid.Misc
{
    public class ProgressStream : Stream
    {
        private Stream m_input = null;
        private long m_length = 0L;
        private long m_position = 0L;
        public event EventHandler<float>? UpdateProgress;

        public ProgressStream(Stream input)
        {
            m_input = input;
            m_length = input.Length;
        }
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int n = await m_input.ReadAsync(buffer, offset, count, cancellationToken);
            m_position += n;
            UpdateProgress?.Invoke(this, (1.0f * m_position) / m_length);
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int n = await m_input.ReadAsync(buffer, cancellationToken);
            m_position += n;
            UpdateProgress?.Invoke(this, (1.0f * m_position) / m_length);
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = m_input.Read(buffer, offset, count);
            m_position += n;
            UpdateProgress?.Invoke(this, (1.0f * m_position) / m_length);
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => m_length;
        public override long Position
        {
            get { return m_position; }
            set { throw new NotImplementedException(); }
        }
    }
}
