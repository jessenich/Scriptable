using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Scriptable.Shell.Utilities;

namespace Scriptable.Shell.Streams {
    /// <summary>
    /// Provides functionality similar to a <see cref="StreamWriter"/> but with additional methods to simplify
    /// working with a process's standard input
    /// </summary>
    public sealed class ProcessStreamWriter : TextWriter {
        private readonly StreamWriter _writer;

        internal ProcessStreamWriter(StreamWriter writer) {
            Throw.IfNull(writer, "writer");
            this._writer = writer;
            this.AutoFlush = true; // set the default
        }

        #region ---- Custom methods ----

        /// <summary>
        /// Provides access to the underlying <see cref="Stream"/>. Equivalent to <see cref="StreamWriter.BaseStream"/>
        /// </summary>
        public Stream BaseStream => this._writer.BaseStream;

        /// <summary>
        /// Determines whether writes are automatically flushed to the underlying <see cref="Stream"/> after each write.
        /// Equivalent to <see cref="StreamWriter.AutoFlush"/>. Defaults to TRUE
        /// </summary>
        public bool AutoFlush {
            get => this._writer.AutoFlush;
            set => this._writer.AutoFlush = value;
        }

        /// <summary>
        /// Asynchronously copies <paramref name="stream"/> to this stream
        /// </summary>
        public Task PipeFromAsync(Stream stream, bool leaveWriterOpen = false, bool leaveStreamOpen = false) {
            Throw.IfNull(stream, "stream");

            return this.PipeAsync(
                async () => {
                    // flush any content buffered in the writer, since we'll be using the raw stream
                    await this._writer.FlushAsync().ConfigureAwait(false);
                    if (this.AutoFlush)
                        // if the writer is configured to autoflush, we preserve that behavior when
                        // piping to the writer from a stream even though for performance we are bypassing
                        // this.writer in this case
                        await stream.CopyToAsyncWithAutoFlush(this.BaseStream).ConfigureAwait(false);
                    else
                        await stream.CopyToAsync(this.BaseStream).ConfigureAwait(false);
                },
                leaveWriterOpen,
                leaveStreamOpen ? default(Action) : () => stream.Dispose()
            );
        }

        /// <summary>
        /// Asynchronously writes each item in <paramref name="lines"/> to this writer as a separate line
        /// </summary>
        public Task PipeFromAsync(IEnumerable<string> lines, bool leaveWriterOpen = false) {
            Throw.IfNull(lines, "lines");

            return this.PipeAsync(
                // wrap in Task.Run since GetEnumerator() or MoveNext() might block
                () => Task.Run(async () => {
                    foreach (var line in lines) await this.WriteLineAsync(line).ConfigureAwait(false);
                }),
                leaveWriterOpen
            );
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="reader"/> to this writer
        /// </summary>
        public Task PipeFromAsync(TextReader reader, bool leaveWriterOpen = false, bool leaveReaderOpen = false) {
            Throw.IfNull(reader, "reader");

            return reader.CopyToAsync(this._writer, leaveReaderOpen, leaveWriterOpen);
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="file"/> to this stream
        /// </summary>
        public Task PipeFromAsync(FileInfo file, bool leaveWriterOpen = false) {
            Throw.IfNull(file, "file");

            var stream = file.OpenRead();
            return this.PipeFromAsync(stream, leaveWriterOpen, false);
        }

        /// <summary>
        /// Asynchronously writes all content from <paramref name="chars"/> to this writer
        /// </summary>
        public Task PipeFromAsync(IEnumerable<char> chars, bool leaveWriterOpen = false) {
            Throw.IfNull(chars, "chars");

            var @string = chars as string;
            return this.PipeAsync(
                @string != null
                    // special-case string since we can use the built-in WriteAsync
                    ? new Func<Task>(() => this.WriteAsync(@string))
                    // when enumerating, layer on a Task.Run since GetEnumerator() or MoveNext() might block
                    : () => Task.Run(async () => {
                        var buffer = new char[Constants.CharBufferSize];
                        using var enumerator = chars.GetEnumerator();
                        while (true) {
                            var i = 0;
                            while (i < buffer.Length && enumerator.MoveNext()) buffer[i++] = enumerator.Current;
                            if (i > 0)
                                await this.WriteAsync(buffer, 0, i).ConfigureAwait(false);
                            else
                                break;
                        }
                    }),
                leaveWriterOpen
            );
        }

        #endregion

        #region ---- TextWriter methods ----

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (disposing) this._writer.Dispose();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Encoding Encoding => this._writer.Encoding;

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Flush() {
            this._writer.Flush();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task FlushAsync() {
            return this._writer.FlushAsync();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override IFormatProvider FormatProvider => this._writer.FormatProvider;

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override string NewLine {
            get => this._writer.NewLine;
            set => this._writer.NewLine = value;
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(bool value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char[] buffer) {
            this._writer.Write(buffer);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(char[] buffer, int index, int count) {
            this._writer.Write(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(decimal value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(double value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(float value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(int value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(long value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(object value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object arg0) {
            this._writer.Write(format, arg0);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object? arg0, object? arg1) {
            this._writer.Write(format, arg0, arg1);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, object? arg0, object? arg1, object? arg2) {
            this._writer.Write(format, arg0, arg1, arg2);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string format, params object[] arg) {
            this._writer.Write(format, arg);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(string value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(uint value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void Write(ulong value) {
            this._writer.Write(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(char value) {
            return this._writer.WriteAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(char[] buffer, int index, int count) {
            return this._writer.WriteAsync(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteAsync(string value) {
            return this._writer.WriteAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine() {
            this._writer.WriteLine();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(bool value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char[] buffer) {
            this._writer.WriteLine(buffer);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(char[] buffer, int index, int count) {
            this._writer.WriteLine(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(decimal value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(double value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(float value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(int value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(long value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(object value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0) {
            this._writer.WriteLine(format, arg0);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0, object arg1) {
            this._writer.WriteLine(format, arg0, arg1);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, object arg0, object arg1, object arg2) {
            this._writer.WriteLine(format, arg0, arg1, arg2);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string format, params object[] arg) {
            this._writer.WriteLine(format, arg);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(string value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(uint value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override void WriteLine(ulong value) {
            this._writer.WriteLine(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync() {
            return this._writer.WriteLineAsync();
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(char value) {
            return this._writer.WriteLineAsync(value);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(char[] buffer, int index, int count) {
            return this._writer.WriteLineAsync(buffer, index, count);
        }

        /// <summary>
        /// Forwards to the implementation in the <see cref="StreamWriter"/> class
        /// </summary>
        public override Task WriteLineAsync(string value) {
            return this._writer.WriteLineAsync(value);
        }

        #endregion
    }
}