﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scriptable;
using Scriptable.Streams;


namespace Scriptable.Test.Streams
{
    using static UnitTestHelpers;

    public class PipeTest
    {
        [Test]
        public void SimpleTest()
        {
            var pipe = new Pipe();

            pipe.WriteText("abc");
            pipe.ReadTextAsync(3).Result.ShouldEqual("abc");

            pipe.WriteText("1");
            pipe.WriteText("2");
            pipe.WriteText("3");
            pipe.ReadTextAsync(3).Result.ShouldEqual("123");

            var asyncRead = pipe.ReadTextAsync(100);
            asyncRead.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false, asyncRead.IsCompleted ? "Found: " + (asyncRead.Result ?? "null") : "not complete");
            pipe.WriteText("x");
            asyncRead.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false, asyncRead.IsCompleted ? "Found: " + (asyncRead.Result ?? "null") : "not complete");
            pipe.WriteText(new string('y', 100));
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true, asyncRead.IsCompleted ? "Found: " + (asyncRead.Result ?? "null") : "not complete");
            asyncRead.Result.ShouldEqual("x" + new string('y', 99));
        }

        [Test]
        public void TestLargeStreamWithFixedLength()
        {
            var pipe = new Pipe();
            pipe.SetFixedLength();

            var bytes = Enumerable.Range(0, 5 * Constants.ByteBufferSize)
                .Select(b => (byte)(b % 256))
                .ToArray();
            var asyncWrite = pipe.InputStream.WriteAsync(bytes, 0, bytes.Length)
                .ContinueWith(_ => pipe.InputStream.Dispose());
            var memoryStream = new MemoryStream();
            var buffer = new byte[777];
            int bytesRead;
            while ((bytesRead = pipe.OutputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
            }
            asyncWrite.Wait(TimeSpan.FromSeconds(1)).ShouldEqual(true);
            memoryStream.ToArray().SequenceEqual(bytes).ShouldEqual(true);
        }

        [Test]
        public void TimeoutTest()
        {
            var pipe = new Pipe { OutputStream = { ReadTimeout = 0 } };
            Assert.Throws<TimeoutException>(() => pipe.OutputStream.ReadByte());

            pipe.WriteText(new string('a', 2048));
            pipe.ReadTextAsync(2048).Result.ShouldEqual(new string('a', 2048));
        }

        [Test]
        public void TestWriteTimeout()
        {
            var pipe = new Pipe { InputStream = { WriteTimeout = 0 } };
            pipe.SetFixedLength();
            pipe.WriteText(new string('a', 2 * Constants.ByteBufferSize));
            var asyncWrite = pipe.InputStream.WriteAsync(new byte[1], 0, 1);
            asyncWrite.ContinueWith(_ => { }).Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(true);
            asyncWrite.IsFaulted.ShouldEqual(true);
            Assert.IsInstanceOf<TimeoutException>(asyncWrite.Exception!.InnerException);
        }

        [Test]
        public void TestPartialWriteDoesNotTimeout()
        {
            var pipe = new Pipe { InputStream = { WriteTimeout = 0 }, OutputStream = { ReadTimeout = 1000 } };
            pipe.SetFixedLength();
            var text = Enumerable.Repeat((byte)'t', 3 * Constants.ByteBufferSize).ToArray();
            var asyncWrite = pipe.InputStream.WriteAsync(text, 0, text.Length);
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);

            pipe.ReadTextAsync(text.Length).Result.ShouldEqual(new string(text.Select(b => (char)b).ToArray()));
        }

        [Test]
        public void TestCancel()
        {
            var pipe = new Pipe();
            var cancellationTokenSource = new CancellationTokenSource();

            var asyncRead = pipe.ReadTextAsync(1, cancellationTokenSource.Token);
            asyncRead.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            cancellationTokenSource.Cancel();
            asyncRead.ContinueWith(_ => { }).Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncRead.IsCanceled.ShouldEqual(true);

            pipe.WriteText("aaa");
            pipe.ReadTextAsync(2).Result.ShouldEqual("aa");

            asyncRead = pipe.ReadTextAsync(1, cancellationTokenSource.Token);
            asyncRead.ContinueWith(_ => { }).Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncRead.IsCanceled.ShouldEqual(true);
        }

        [Test]
        public void TestCancelWrite()
        {
            var pipe = new Pipe();
            var cancellationTokenSource = new CancellationTokenSource();

            pipe.WriteText(new string('a', 2 * Constants.ByteBufferSize));

            pipe.SetFixedLength();

            var bytes = Enumerable.Repeat((byte)'b', Constants.ByteBufferSize).ToArray();
            var asyncWrite = pipe.InputStream.WriteAsync(bytes, 0, bytes.Length, cancellationTokenSource.Token);
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            cancellationTokenSource.Cancel();
            asyncWrite.ContinueWith(_ => { }).Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncWrite.IsCanceled.ShouldEqual(true);
        }

        [Test]
        public void TestPartialWriteDoesNotCancel()
        {
            var pipe = new Pipe();
            var cancellationTokenSource = new CancellationTokenSource();

            pipe.WriteText(new string('a', 2 * Constants.ByteBufferSize));

            var asyncRead = pipe.ReadTextAsync(1);
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncRead.Result.ShouldEqual("a");

            pipe.SetFixedLength();

            var bytes = Enumerable.Repeat((byte)'b', Constants.ByteBufferSize).ToArray();
            var asyncWrite = pipe.InputStream.WriteAsync(bytes, 0, bytes.Length, cancellationTokenSource.Token);
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            cancellationTokenSource.Cancel();
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);

            asyncRead = pipe.ReadTextAsync((3 * Constants.ByteBufferSize) - 1);
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncRead.Result.ShouldEqual(new string('a', (2 * Constants.ByteBufferSize) - 1) + new string('b', Constants.ByteBufferSize));
        }

        [Test]
        public void TestCloseWriteSide()
        {
            var pipe = new Pipe();
            pipe.WriteText("123456");
            pipe.InputStream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => pipe.InputStream.WriteByte(1));

            pipe.ReadTextAsync(5).Result.ShouldEqual("12345");
            pipe.ReadTextAsync(2).Result.ShouldEqual(null);

            pipe = new Pipe();
            var asyncRead = pipe.ReadTextAsync(1);
            asyncRead.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            pipe.InputStream.Dispose();
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
        }

        [Test]
        public void TestCloseReadSide()
        {
            var pipe = new Pipe();
            pipe.WriteText("abc");
            pipe.ReadTextAsync(2).Result.ShouldEqual("ab");
            pipe.OutputStream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => pipe.OutputStream.ReadByte());

            var largeBytes = new byte[10 * 1024];
            var initialMemory = GC.GetTotalMemory(forceFullCollection: true);
            for (var i = 0; i < int.MaxValue / 1024; ++i)
            {
                pipe.InputStream.Write(largeBytes, 0, largeBytes.Length);
            }
            var finalMemory = GC.GetTotalMemory(forceFullCollection: true);

            (finalMemory - initialMemory < 10 * largeBytes.Length)
                .ShouldEqual(true, "final = " + finalMemory + " initial = " + initialMemory);

            Assert.Throws<ObjectDisposedException>(() => pipe.OutputStream.ReadByte());

            pipe.InputStream.Dispose();
        }

        [Test]
        public void TestConcurrentReads()
        {
            var pipe = new Pipe();

            var asyncRead = pipe.ReadTextAsync(1);
            Assert.Throws<InvalidOperationException>(() => pipe.OutputStream.ReadByte());
            pipe.InputStream.Dispose();
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
        }

        [Test]
        public void TestConcurrentWrites()
        {
            var pipe = new Pipe();
            pipe.SetFixedLength();

            var longText = new string('x', (2 * Constants.ByteBufferSize) + 1);
            var asyncWrite = pipe.WriteTextAsync(longText);
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            Assert.Throws<InvalidOperationException>(() => pipe.InputStream.WriteByte(101));
            pipe.OutputStream.Dispose();
            asyncWrite.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
        }

        [Test]
        public void TestChainedPipes()
        {
            var pipes = CreatePipeChain(100);

            // short write
            pipes[0].InputStream.WriteByte(100);
            var buffer = new byte[1];
            pipes.Last().OutputStream.ReadAsync(buffer, 0, buffer.Length)
                .Wait(TimeSpan.FromSeconds(5))
                .ShouldEqual(true);

            buffer[0].ShouldEqual((byte)100);

            // long write
            var longText = new string('y', 3 * Constants.CharBufferSize);
            var asyncWrite = pipes[0].WriteTextAsync(longText);
            asyncWrite.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            var asyncRead = pipes.Last().ReadTextAsync(longText.Length);
            asyncRead.Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);
            asyncRead.Result.ShouldEqual(longText);
        }

        [Test]
        public void TestPipeChainWithFixedLengthPipes()
        {
            var pipes = CreatePipeChain(2);
            // note that this needs to be >> larger than the capacity to block all the pipes,
            // since we can store bytes in each pipe in the chain + each buffer between pipes
            var longText = new string('z', (8 * Constants.ByteBufferSize) + 1);
            pipes.ForEach(p => p.SetFixedLength());
            var asyncWrite = pipes[0].WriteTextAsync(longText);
            asyncWrite.Wait(TimeSpan.FromSeconds(.01)).ShouldEqual(false);
            var asyncRead = pipes.Last().ReadTextAsync(longText.Length);
            asyncWrite.Wait(TimeSpan.FromSeconds(10)).ShouldEqual(true);
            asyncRead.Wait(TimeSpan.FromSeconds(10)).ShouldEqual(true);
            asyncRead.Result.ShouldNotEqual(null);
            asyncRead.Result!.Length.ShouldEqual(longText.Length);
            asyncRead.Result.ShouldEqual(longText);
        }

        private static List<Pipe> CreatePipeChain(int length)
        {
            var pipes = Enumerable.Range(0, length).Select(_ => new Pipe())
                .ToList();
            for (var i = 0; i < pipes.Count - 1; ++i)
            {
                var fromPipe = pipes[i];
                var toPipe = pipes[i + 1];
                fromPipe.OutputStream.CopyToAsync(toPipe.InputStream)
                    .ContinueWith(_ =>
                    {
                        fromPipe.OutputStream.Dispose();
                        toPipe.InputStream.Dispose();
                    });
            }

            return pipes;
        }

        [Test]
        public void FuzzTest()
        {
            const int ByteCount = 100000;

            var pipe = new Pipe();
            var writeTask = Task.Run(async () =>
            {
                var memoryStream = new MemoryStream();
                var random = new Random(1234);
                var bytesWritten = 0;
                while (bytesWritten < ByteCount)
                {
                    switch (random.Next(10))
                    {
                        case 1:
                            pipe.SetFixedLength();
                            break;
                        case 2:
                        case 3:
                            await Task.Delay(1);
                            break;
                        default:
                            var bufferLength = random.Next(0, 5000);
                            var offset = random.Next(0, bufferLength + 1);
                            var count = random.Next(0, bufferLength - offset + 1);
                            var buffer = new byte[bufferLength];
                            random.NextBytes(buffer);
                            memoryStream.Write(buffer, offset, count);
                            await pipe.InputStream.WriteAsync(buffer, offset, count);
                            bytesWritten += count;
                            break;
                    }
                }
                pipe.InputStream.Dispose();
                return memoryStream;
            });

            var readTask = Task.Run(async () =>
            {
                var memoryStream = new MemoryStream();
                var random = new Random(5678);
                while (true)
                {
                    if (random.Next(10) == 1)
                    {
                        await Task.Delay(1);
                    }
                    var bufferLength = random.Next(0, 5000);
                    var offset = random.Next(0, bufferLength + 1);
                    var count = random.Next(0, bufferLength - offset + 1);
                    var buffer = new byte[bufferLength];
                    var bytesRead = await pipe.OutputStream.ReadAsync(buffer, offset, count);
                    if (bytesRead == 0 && count > 0)
                    {
                        // if we tried to read more than 1 byte and we got 0, the pipe is done
                        return memoryStream;
                    }
                    memoryStream.Write(buffer, offset, bytesRead);
                }
            });

            Task.WhenAll(writeTask, readTask).Wait(TimeSpan.FromSeconds(5)).ShouldEqual(true);

            writeTask.Result.ToArray().SequenceEqual(readTask.Result.ToArray())
                .ShouldEqual(true);
        }
    }

    internal static class PipeExtensions
    {
        public static void WriteText(this Pipe @this, string text)
        {
            new StreamWriter(@this.InputStream) { AutoFlush = true }.Write(text);
        }

        public static Task WriteTextAsync(this Pipe @this, string text)
        {
            return new StreamWriter(@this.InputStream) { AutoFlush = true }.WriteAsync(text);
        }

        public static async Task<string?> ReadTextAsync(this Pipe @this, int count, CancellationToken token = default)
        {
            var bytes = new byte[count];
            var bytesRead = 0;
            while (bytesRead < count)
            {
                var result = await @this.OutputStream.ReadAsync(bytes, offset: bytesRead, count: count - bytesRead, cancellationToken: token);
                if (result == 0) { return null; }
                bytesRead += result;
            }

            return new string(Encoding.UTF8.GetChars(bytes));
        }
    }
}
