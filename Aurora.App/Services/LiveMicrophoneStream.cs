using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Aurora.App.Services
{
    /// <summary>Bridges NAudio's callback-based microphone capture into a plain
    /// <see cref="Stream"/> that <see cref="System.Speech.Recognition.SpeechRecognitionEngine"/>
    /// can read from via SetInputToAudioStream - the recognizer only knows how to pull PCM
    /// bytes from a Stream, while NAudio only knows how to push captured bytes via an event,
    /// so this queues the pushed chunks and lets the recognizer's read thread block until
    /// more audio arrives (or capture is stopped).</summary>
    public sealed class LiveMicrophoneStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks = new();
        private byte[] _current = Array.Empty<byte>();
        private int _currentOffset;
        private long _position;

        /// <summary>Called from the NAudio capture callback thread whenever a new buffer of
        /// microphone audio is available.</summary>
        public void Push(byte[] data)
        {
            if (data.Length == 0) return;
            try { _chunks.Add(data); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { /* stream already completed or disposed */ }
        }

        /// <summary>Signals no more audio is coming - unblocks a pending Read once any
        /// already-queued audio has been consumed.</summary>
        public void Complete() => _chunks.CompleteAdding();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                if (_currentOffset >= _current.Length)
                {
                    if (!_chunks.TryTake(out var next, Timeout.Infinite))
                    {
                        _current = Array.Empty<byte>();
                        _currentOffset = 0;
                        break; // capture stopped and every queued chunk has been consumed
                    }
                    _current = next;
                    _currentOffset = 0;
                }

                var toCopy = Math.Min(count - total, _current.Length - _currentOffset);
                Array.Copy(_current, _currentOffset, buffer, offset + total, toCopy);
                _currentOffset += toCopy;
                total += toCopy;
                Interlocked.Add(ref _position, toCopy);
            }
            return total;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        // System.Speech's SAPI wrapper reads Length even for non-seekable input,
        // and uses Position to bound each read. Live audio has no known end;
        // Complete(), rather than a snapshot of the queued bytes, signals EOF.
        public override long Length => long.MaxValue;
        public override long Position { get => Interlocked.Read(ref _position); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) =>
            offset == 0 && origin == SeekOrigin.Current ? Position : throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_chunks.IsAddingCompleted) _chunks.CompleteAdding();
                _chunks.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
