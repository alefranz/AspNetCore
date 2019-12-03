using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.ResponseCaching.Tests
{
    public class CachedResponseBodyTests
    {
        private readonly int _timeout = Debugger.IsAttached ? -1 : 500;

        [Fact]
        public async Task Copy_DoNothingWhenNoSegments()
        {
            var segments = new List<byte[]>();
            var receivedSegments = new List<byte[]>();
            var body = new CachedResponseBody(segments, 0);

            var pipe = new Pipe();
            using var cts = new CancellationTokenSource(_timeout);

            var receiverTask = ReceiveDataAsync(pipe.Reader, receivedSegments, cts.Token);
            var copyTask = body.CopyToAsync(pipe.Writer, CancellationToken.None).ContinueWith(_ => pipe.Writer.CompleteAsync());

            await Task.WhenAll(receiverTask, copyTask);

            Assert.Empty(receivedSegments);
        }

        [Fact]
        public async Task Copy_SingleSegment()
        {
            var segments = new List<byte[]>
            {
                new byte[] { 1 }
            };
            var receivedSegments = new List<byte[]>();
            var body = new CachedResponseBody(segments, 0);

            var pipe = new Pipe();

            using var cts = new CancellationTokenSource(_timeout);

            var receiverTask = ReceiveDataAsync(pipe.Reader, receivedSegments, cts.Token);
            var copyTask = CopyDataAsync(body, pipe.Writer, cts.Token);

            await Task.WhenAll(receiverTask, copyTask);

            Assert.Equal(segments, receivedSegments);
        }

        [Fact]
        public async Task Copy_MultipleSegments()
        {
            var segments = new List<byte[]>
            {
                new byte[] { 1 },
                new byte[] { 2, 3 }
            };
            var receivedSegments = new List<byte[]>();
            var body = new CachedResponseBody(segments, 0);

            var pipe = new Pipe();

            using var cts = new CancellationTokenSource(_timeout);

            var receiverTask = ReceiveDataAsync(pipe.Reader, receivedSegments, cts.Token);
            var copyTask = CopyDataAsync(body, pipe.Writer, cts.Token);

            await Task.WhenAll(receiverTask, copyTask);

            Assert.Equal(new byte[] { 1, 2, 3 }, receivedSegments.SelectMany(x => x).ToArray());
        }

        async Task CopyDataAsync(CachedResponseBody body, PipeWriter writer, CancellationToken cancellationToken)
        {
            await body.CopyToAsync(writer, cancellationToken);
            await writer.CompleteAsync();
        }

        async Task ReceiveDataAsync(PipeReader reader, List<byte[]> receivedSegments, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                foreach(var memory in buffer)
                {
                    receivedSegments.Add(memory.ToArray());
                }

                if (result.IsCompleted)
                {
                    break;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
            await reader.CompleteAsync();
        }
    }
}
