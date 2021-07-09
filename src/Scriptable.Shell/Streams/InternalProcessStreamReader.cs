using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Scriptable.Shell.Streams {
    internal sealed class InternalProcessStreamReader : ProcessStreamReader {
        /// <summary>
        /// The underlying <see cref="Stream"/> from the <see cref="Process"/>
        /// </summary>
        private readonly Stream _processStream;

        private readonly Pipe _pipe;
        private readonly StreamReader _reader;
        private volatile bool _discardContents;

        public InternalProcessStreamReader(StreamReader processStreamReader) {
            this._processStream = processStreamReader.BaseStream;
            this._pipe = new Pipe();
            this._reader = new StreamReader(this._pipe.OutputStream, processStreamReader.CurrentEncoding);
            this.Task = Task.Run(() => this.BufferLoop());
        }

        public Task Task { get; }

        private async Task BufferLoop() {
            try {
                var buffer = new byte[Constants.ByteBufferSize];
                int bytesRead;
                while (
                    !this._discardContents
                 && (bytesRead = await this._processStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0
                )
                    await this._pipe.InputStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
            }
            finally {
                #if NETSTANDARD1_3
                this.processStream.Dispose();
                this.pipe.InputStream.Dispose();
                #else
                this._processStream.Close();
                this._pipe.InputStream.Close();
                #endif
            }
        }

        #region ---- ProcessStreamReader implementation ----

        public override Stream BaseStream => this._reader.BaseStream;

        public override Encoding Encoding => this._reader.CurrentEncoding;

        public override void Discard() {
            this._discardContents = true;
            this._reader.Dispose();
        }

        public override void StopBuffering() {
            // this causes writes to the pipe to block, thus
            // preventing unbounded buffering (although some more content
            // may still be buffered)
            this._pipe.SetFixedLength();
        }

        #endregion

        #region ---- TextReader implementation ----

        // all reader methods are overriden to call the same method on the underlying StreamReader.
        // This approach is preferable to extending StreamReader directly, since many of the async methods
        // on StreamReader are conservative and fall back to threaded asynchrony when inheritance is in play
        // (this is done to respect any overriden Read() call). This way, we get the full benefit of asynchrony.

        public override int Peek() {
            return this._reader.Peek();
        }

        public override int Read() {
            return this._reader.Read();
        }

        public override int Read(char[] buffer, int index, int count) {
            return this._reader.Read(buffer, index, count);
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count) {
            return this._reader.ReadAsync(buffer, index, count);
        }

        public override int ReadBlock(char[] buffer, int index, int count) {
            return this._reader.ReadBlock(buffer, index, count);
        }

        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count) {
            return this._reader.ReadBlockAsync(buffer, index, count);
        }

        public override string ReadLine() {
            return this._reader.ReadLine();
        }

        public override Task<string> ReadLineAsync() {
            return this._reader.ReadLineAsync();
        }

        public override string ReadToEnd() {
            return this._reader.ReadToEnd();
        }

        public override Task<string> ReadToEndAsync() {
            return this._reader.ReadToEndAsync();
        }

        protected override void Dispose(bool disposing) {
            if (disposing) this.Discard();
        }

        #endregion
    }
}