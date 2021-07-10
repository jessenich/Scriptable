using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Scriptable.Utilities;

namespace Scriptable.Streams {
    internal sealed class Pipe {
        // From MemoryStream (see http://referencesource.microsoft.com/#mscorlib/system/io/memorystream.cs,1416df83d2368912)
        private const int MinSize = 256;

        /// <summary>
        /// The maximum size at which the pipe will be left empty. Using 2 * <see cref="Constants.ByteBufferSize"/>
        /// helps prevent thrashing if data is being pushed through the pipe at that rate
        /// </summary>
        private const int MaxStableSize = 2 * Constants.ByteBufferSize;

        private static readonly byte[] Empty = new byte[0];
        private static readonly Task<int> CompletedZeroTask = Task.FromResult(0);

        private readonly SemaphoreSlim _bytesAvailableSignal = new(0, 1);
        private readonly object _lock = new();
        private readonly PipeInputStream _input;
        private readonly PipeOutputStream _output;

        private byte[] _buffer = Empty;
        private int _start;
        private int _count;
        private bool _writerClosed;
        private bool _readerClosed;
        private SemaphoreSlim? _spaceAvailableSignal;
        private Task<int> _readTask = CompletedZeroTask;
        private Task _writeTask = CompletedZeroTask;

        public Pipe() {
            this._input = new PipeInputStream(this);
            this._output = new PipeOutputStream(this);
        }

        public Stream InputStream => this._input;
        public Stream OutputStream => this._output;

        #region ---- Signals ----

        public void SetFixedLength() {
            lock (this._lock) {
                if (this._spaceAvailableSignal == null
                 && !this._readerClosed
                 && !this._writerClosed)
                    this._spaceAvailableSignal = new SemaphoreSlim(
                        this.GetSpaceAvailableNoLock() > 0 ? 1 : 0,
                        1
                    );
            }
        }

        private int GetSpaceAvailableNoLock() {
            return Math.Max(this._buffer.Length, MaxStableSize) - this._count;
        }

        /// <summary>
        /// MA: I used to have the signals updated in various ways and in various places
        /// throughout the code. Now I have just one function that sets both signals to the correct
        /// values. This is called from <see cref="ReadNoLock"/>, <see cref="WriteNoLock"/>,
        /// <see cref="InternalCloseReadSideNoLock"/>, and <see cref="InternalCloseWriteSideNoLock"/>.
        ///
        /// While it may seem like this does extra work, nearly all cases are necessary. For example, we used
        /// to say "signal bytes available if count > 0" at the end of <see cref="WriteNoLock"/>. The problem is
        /// that we could have the following sequence of operations:
        /// 1. <see cref="ReadNoLockAsync"/> blocks on <see cref="_bytesAvailableSignal"/>
        /// 2. <see cref="WriteNoLock"/> writes and signals
        /// 3. <see cref="ReadNoLockAsync"/> wakes up
        /// 4. Another <see cref="WriteNoLock"/> call writes and re-signals
        /// 5. <see cref="ReadNoLockAsync"/> reads ALL content and returns, leaving <see cref="_bytesAvailableSignal"/> signaled (invalid)
        ///
        /// This new implementation avoids this because the <see cref="ReadNoLock"/> call inside <see cref="ReadNoLockAsync"/> will
        /// properly unsignal after it consumes ALL the contents
        /// </summary>
        private void UpdateSignalsNoLock() {
            // update bytes available
            switch (this._bytesAvailableSignal.CurrentCount) {
                case 0:
                    if (this._count > 0 || this._writerClosed) this._bytesAvailableSignal.Release();
                    break;
                case 1:
                    if (this._count == 0 && !this._writerClosed) this._bytesAvailableSignal.Wait();
                    break;
                default:
                    throw new InvalidOperationException("Should never get here");
            }

            switch (this._spaceAvailableSignal?.CurrentCount) {
                case 0:
                    if (this._readerClosed || this.GetSpaceAvailableNoLock() > 0)
                        this._spaceAvailableSignal?.Release();
                    break;
                case 1:
                    if (!this._readerClosed && this.GetSpaceAvailableNoLock() == 0)
                        this._spaceAvailableSignal?.Wait();
                    break;
                default:
                    throw new InvalidOperationException("Should never get here");
            }
        }

        #endregion

        #region ---- Writing ----

        private Task WriteAsync(byte[] buffer, int offset, int count, TimeSpan timeout, CancellationToken cancellationToken) {
            Throw.IfInvalidBuffer(buffer, offset, count);

            // always respect cancellation, even in the sync flow
            if (cancellationToken.IsCancellationRequested) return CreateCanceledTask();

            if (count == 0)
                // if we didn't want to write anything, return immediately
                return CompletedZeroTask;

            lock (this._lock) {
                Throw<ObjectDisposedException>.If(this._writerClosed, "The write side of the pipe is closed");
                Throw<InvalidOperationException>.If(!this._writeTask.IsCompleted, "Concurrent writes are not allowed");

                if (this._readerClosed)
                    // if we can't read, just throw away the bytes since no one can observe them anyway
                    return CompletedZeroTask;

                if (this._spaceAvailableSignal == null
                 || this.GetSpaceAvailableNoLock() >= count) {
                    // if we're not limited by space, just write and return
                    this.WriteNoLock(buffer, offset, count);

                    return CompletedZeroTask;
                }

                // otherwise, create and return an async write task
                return this._writeTask = this.WriteNoLockAsync(buffer, offset, count, timeout, cancellationToken);
            }
        }

        private async Task WriteNoLockAsync(byte[] buffer, int offset, int count, TimeSpan timeout, CancellationToken cancellationToken) {
            var remainingCount = count;
            do {
                // MA: we only use the timeout/token on the first time through, to avoid doing part of the write. This way, it's all or nothing
                CancellationToken cancellationTokenToUse;
                TimeSpan timeoutToUse;
                if (remainingCount == count) {
                    timeoutToUse = timeout;
                    cancellationTokenToUse = cancellationToken;
                }
                else {
                    timeoutToUse = Timeout.InfiniteTimeSpan;
                    cancellationTokenToUse = CancellationToken.None;
                }

                // acquire the semaphore
                var acquired = await this._spaceAvailableSignal!.WaitAsync(timeoutToUse, cancellationTokenToUse).ConfigureAwait(false);
                if (!acquired) throw new TimeoutException("Timed out writing to the pipe");

                // we need to reacquire the lock after the await since we might have switched threads
                lock (this._lock) {
                    if (this._readerClosed) {
                        // if the read side is gone, we're instantly done
                        remainingCount = 0;
                    }
                    else {
                        var countToWrite = Math.Min(this.GetSpaceAvailableNoLock(), remainingCount);
                        this.WriteNoLock(buffer, offset + (count - remainingCount), countToWrite);

                        remainingCount -= countToWrite;
                    }
                }
            } while (remainingCount > 0);
        }

        private void WriteNoLock(byte[] buffer, int offset, int count) {
            if (count <= 0) throw new InvalidOperationException("Sanity check: WriteNoLock requires positive count");

            this.EnsureCapacityNoLock(unchecked(this._count + count));

            var writeStart = (this._start + this._count) % this._buffer.Length;
            var writeStartToEndCount = Math.Min(this._buffer.Length - writeStart, count);
            Buffer.BlockCopy(buffer, offset, this._buffer, writeStart, writeStartToEndCount);
            Buffer.BlockCopy(buffer, offset + writeStartToEndCount, this._buffer, 0, count - writeStartToEndCount);
            this._count += count;

            this.UpdateSignalsNoLock();
        }

        private void EnsureCapacityNoLock(int capacity) {
            if (capacity < 0) throw new IOException("Pipe stream is too long");

            var currentCapacity = this._buffer.Length;
            if (capacity <= currentCapacity) return;

            if (this._spaceAvailableSignal != null
             && capacity > MaxStableSize)
                throw new InvalidOperationException("Sanity check: pipe should not attempt to expand beyond stable size in fixed length mode");

            int newCapacity;
            if (currentCapacity < MinSize) {
                newCapacity = Math.Max(capacity, MinSize);
            }
            else {
                var doubleCapacity = 2L * currentCapacity;
                newCapacity = capacity >= doubleCapacity
                    ? capacity
                    : (int) Math.Min(doubleCapacity, int.MaxValue);
            }

            var newBuffer = new byte[newCapacity];
            var startToEndCount = Math.Min(this._buffer.Length - this._start, this._count);
            Buffer.BlockCopy(this._buffer, this._start, newBuffer, 0, startToEndCount);
            Buffer.BlockCopy(this._buffer, 0, newBuffer, startToEndCount, this._count - startToEndCount);
            this._buffer = newBuffer;
            this._start = 0;
        }

        private void CloseWriteSide() {
            lock (this._lock) {
                // no-op if we're already closed
                if (this._writerClosed) return;

                // if we don't have an active write task, close now
                if (this._writeTask.IsCompleted) {
                    this.InternalCloseWriteSideNoLock();
                    return;
                }

                // otherwise, close as a continuation on the write task
                this._writeTask = this._writeTask.ContinueWith(
                    (t, state) => {
                        var @this = (Pipe?)state;
                        if (@this == null) {
                            throw new ArgumentNullException($"{nameof(state)}.{nameof(@this._lock)}");
                        }
                        lock (@this._lock) {
                            @this.InternalCloseWriteSideNoLock();
                        }
                    },
                    this
                );
            }
        }

        private void InternalCloseWriteSideNoLock() {
            this._writerClosed = true;
            this.UpdateSignalsNoLock();
            if (this._readerClosed)
                // if both sides are now closed, cleanup
                this.CleanupNoLock();
        }

        #endregion

        #region ---- Reading ----

        private Task<int> ReadAsync(byte[] buffer, int offset, int count, TimeSpan timeout, CancellationToken cancellationToken) {
            Throw.IfInvalidBuffer(buffer, offset, count);

            // always respect cancellation, even in the sync flow
            if (cancellationToken.IsCancellationRequested) return CreateCanceledTask();

            // if we didn't want to read anything, return immediately
            if (count == 0) return CompletedZeroTask;

            lock (this._lock) {
                Throw<ObjectDisposedException>.If(this._readerClosed, "The read side of the pipe is closed");
                Throw<InvalidOperationException>.If(!this._readTask.IsCompleted, "Concurrent reads are not allowed");

                // if we have bytes, read them and return synchronously
                if (this._count > 0) return Task.FromResult(this.ReadNoLock(buffer, offset, count));

                // if we don't have bytes and no more are coming, return 0
                if (this._writerClosed) return CompletedZeroTask;

                // otherwise, create and return an async read task
                return this._readTask = this.ReadNoLockAsync(buffer, offset, count, timeout, cancellationToken);
            }
        }

        private async Task<int> ReadNoLockAsync(byte[] buffer, int offset, int count, TimeSpan timeout, CancellationToken cancellationToken) {
            var acquired = await this._bytesAvailableSignal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (!acquired) throw new TimeoutException("Timed out reading from the pipe");

            // we need to reacquire the lock after the await since we might have switched threads
            lock (this._lock) {
                return this.ReadNoLock(buffer, offset, count);
            }
        }

        private int ReadNoLock(byte[] buffer, int offset, int count) {
            var countToRead = Math.Min(this._count, count);

            var bytesRead = 0;
            while (bytesRead < countToRead) {
                var bytesToRead = Math.Min(countToRead - bytesRead, this._buffer.Length - this._start);
                Buffer.BlockCopy(this._buffer, this._start, buffer, offset + bytesRead, bytesToRead);
                bytesRead += bytesToRead;
                this._start = (this._start + bytesToRead) % this._buffer.Length;
            }

            this._count -= countToRead;

            // ensure that an empty pipe never stays above the max stable size
            if (this._count == 0
             && this._buffer.Length > MaxStableSize) {
                this._start = 0;
                this._buffer = new byte[MaxStableSize];
            }

            this.UpdateSignalsNoLock();

            return countToRead;
        }

        private void CloseReadSide() {
            lock (this._lock) {
                // no-op if we're already closed
                if (this._readerClosed) return;

                // if we don't have an active read task, close now
                if (this._readTask.IsCompleted) {
                    this.InternalCloseReadSideNoLock();
                    return;
                }

                // otherwise, close as a continuation on the read task
                this._readTask = this._readTask.ContinueWith(
                    (t, state) => {
                        var @this = (Pipe) state;
                        lock (@this._lock) {
                            @this.InternalCloseReadSideNoLock();
                        }

                        return -1;
                    },
                    this
                );
            }
        }

        private void InternalCloseReadSideNoLock() {
            this._readerClosed = true;
            this.UpdateSignalsNoLock();
            if (this._writerClosed)
                // if both sides are now closed, cleanup
                this.CleanupNoLock();
        }

        #endregion

        #region ---- Dispose ----

        private void CleanupNoLock() {
            this._buffer = Empty;
            this._writeTask = this._readTask = CompletedZeroTask;
            this._bytesAvailableSignal.Dispose();
            this._spaceAvailableSignal?.Dispose();
        }

        #endregion

        #region ---- Cancellation ----

        private static Task<int> CreateCanceledTask() {
            var taskCompletionSource = new TaskCompletionSource<int>();
            taskCompletionSource.SetCanceled();
            return taskCompletionSource.Task;
        }

        #endregion

        #region ---- Input Stream ----

        private sealed class PipeInputStream : Stream {
            private readonly Pipe _pipe;

            public PipeInputStream(Pipe pipe) {
                this._pipe = pipe;
            }

            #if !NETSTANDARD1_3
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
                throw WriteOnly();
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
                // according to the docs, the callback is optional
                var writeTask = this.WriteAsync(buffer, offset, count, CancellationToken.None);
                var writeResult = new AsyncWriteResult(state, writeTask, this);
                if (callback != null) writeTask.ContinueWith(_ => callback(writeResult));
                return writeResult;
            }
            #endif

            private sealed class AsyncWriteResult : IAsyncResult {
                private readonly object _state;

                public AsyncWriteResult(object state, Task writeTask, PipeInputStream stream) {
                    this._state = state;
                    this.WriteTask = writeTask;
                    this.Stream = stream;
                }

                public Task WriteTask { get; }

                public Stream Stream { get; }

                object IAsyncResult.AsyncState => this._state;

                WaitHandle IAsyncResult.AsyncWaitHandle => this.WriteTask.As<IAsyncResult>().AsyncWaitHandle;

                bool IAsyncResult.CompletedSynchronously => this.WriteTask.As<IAsyncResult>().CompletedSynchronously;

                bool IAsyncResult.IsCompleted => this.WriteTask.IsCompleted;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanTimeout => true;
            public override bool CanWrite => true;

            #if !NETSTANDARD1_3
            public override void Close() {
                base.Close(); // calls Dispose(true)
            }
            #endif

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
                throw WriteOnly();
            }

            protected override void Dispose(bool disposing) {
                if (disposing) this._pipe.CloseWriteSide();
            }

            #if !NETSTANDARD1_3
            public override int EndRead(IAsyncResult asyncResult) {
                throw WriteOnly();
            }

            public override void EndWrite(IAsyncResult asyncResult) {
                Throw.IfNull(asyncResult, nameof(asyncResult));
                var writeResult = asyncResult as AsyncWriteResult
                               ?? throw new ArgumentException("must be created by this stream's BeginWrite method", nameof(asyncResult));
                writeResult.WriteTask.Wait();
            }
            #endif

            public override void Flush() {
                // no-op, since we are just a buffer
            }

            public override Task FlushAsync(CancellationToken cancellationToken) {
                // no-op since we are just a buffer
                return CompletedZeroTask;
            }

            public override long Length => throw Throw.NotSupported();

            public override long Position {
                get => throw Throw.NotSupported();
                set => throw Throw.NotSupported();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                throw WriteOnly();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                throw WriteOnly();
            }

            public override int ReadByte() {
                throw WriteOnly();
            }

            public override int ReadTimeout {
                get => throw WriteOnly();
                set => throw WriteOnly();
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw Throw.NotSupported();
            }

            public override void SetLength(long value) {
                throw Throw.NotSupported();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                try {
                    this._pipe.WriteAsync(buffer, offset, count, TimeSpan.FromMilliseconds(this.WriteTimeout), CancellationToken.None).Wait();
                }
                catch (AggregateException ex) {
                    // unwrap aggregate if we can
                    ExceptionDispatchInfo.Capture(ex.GetBaseException()).Throw();

                    throw;
                }
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                return this._pipe.WriteAsync(buffer, offset, count, TimeSpan.FromMilliseconds(this.WriteTimeout), cancellationToken);
            }

            public override void WriteByte(byte value) {
                // the base implementation is inefficient, but I don't think we care
                base.WriteByte(value);
            }

            private int _writeTimeout = Timeout.Infinite;

            public override int WriteTimeout {
                get => this._writeTimeout;
                set {
                    if (value != Timeout.Infinite) Throw.IfOutOfRange(value, "WriteTimeout", 0);
                    this._writeTimeout = value;
                }
            }

            private static NotSupportedException WriteOnly([CallerMemberName] string memberName = "") {
                throw new NotSupportedException(memberName + ": the stream is write only");
            }
        }

        #endregion

        #region ---- Output Stream ----

        private sealed class PipeOutputStream : Stream {
            private readonly Pipe _pipe;

            public PipeOutputStream(Pipe pipe) {
                this._pipe = pipe;
            }

            #if !NETSTANDARD1_3
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
                // according to the docs, the callback is optional
                var readTask = this.ReadAsync(buffer, offset, count, CancellationToken.None);
                var readResult = new AsyncReadResult(state, readTask, this);
                if (callback != null) readTask.ContinueWith(_ => callback(readResult));
                return readResult;
            }
            #endif

            private sealed class AsyncReadResult : IAsyncResult {
                private readonly object _state;

                public AsyncReadResult(object state, Task<int> readTask, PipeOutputStream stream) {
                    this._state = state;
                    this.ReadTask = readTask;
                    this.Stream = stream;
                }

                public Task<int> ReadTask { get; }

                public Stream Stream { get; }

                object IAsyncResult.AsyncState => this._state;

                WaitHandle IAsyncResult.AsyncWaitHandle => this.ReadTask.As<IAsyncResult>().AsyncWaitHandle;

                bool IAsyncResult.CompletedSynchronously => this.ReadTask.As<IAsyncResult>().CompletedSynchronously;

                bool IAsyncResult.IsCompleted => this.ReadTask.IsCompleted;
            }

            #if !NETSTANDARD1_3
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
                throw ReadOnly();
            }
            #endif

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanTimeout => true;
            public override bool CanWrite => false;

            #if !NETSTANDARD1_3
            public override void Close() {
                base.Close(); // calls Dispose(true)
            }
            #endif

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
                // the base implementation is reasonable
                return base.CopyToAsync(destination, bufferSize, cancellationToken);
            }

            protected override void Dispose(bool disposing) {
                if (disposing) this._pipe.CloseReadSide();
            }

            #if !NETSTANDARD1_3
            public override int EndRead(IAsyncResult asyncResult) {
                Throw.IfNull(asyncResult, nameof(asyncResult));
                var readResult = asyncResult as AsyncReadResult
                              ?? throw new ArgumentException("must be created by this stream's BeginRead method", nameof(asyncResult));
                return readResult.ReadTask.Result;
            }

            public override void EndWrite(IAsyncResult asyncResult) {
                throw ReadOnly();
            }
            #endif

            public override void Flush() {
                throw ReadOnly();
            }

            public override Task FlushAsync(CancellationToken cancellationToken) {
                throw ReadOnly();
            }

            public override long Length => throw Throw.NotSupported();

            public override long Position {
                get => throw Throw.NotSupported();
                set => throw Throw.NotSupported();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                try {
                    return this._pipe.ReadAsync(buffer, offset, count, TimeSpan.FromMilliseconds(this.ReadTimeout), CancellationToken.None).Result;
                }
                catch (AggregateException ex) {
                    // unwrap aggregate if we can
                    if (ex.InnerExceptions.Count == 1) ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

                    throw;
                }
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                return this._pipe.ReadAsync(buffer, offset, count, TimeSpan.FromMilliseconds(this.ReadTimeout), cancellationToken);
            }

            public override int ReadByte() {
                // this is inefficient, but I think that's ok
                return base.ReadByte();
            }

            private int _readTimeout = Timeout.Infinite;

            public override int ReadTimeout {
                get => this._readTimeout;
                set {
                    if (value != Timeout.Infinite) Throw.IfOutOfRange(value, "ReadTimeout", 0);
                    this._readTimeout = value;
                }
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw Throw.NotSupported();
            }

            public override void SetLength(long value) {
                throw Throw.NotSupported();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                throw ReadOnly();
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                throw ReadOnly();
            }

            public override void WriteByte(byte value) {
                throw ReadOnly();
            }

            public override int WriteTimeout {
                get => throw ReadOnly();
                set => throw ReadOnly();
            }

            private static NotSupportedException ReadOnly([CallerMemberName] string memberName = "") {
                throw new NotSupportedException(memberName + ": the stream is read only");
            }
        }

        #endregion
    }
}
