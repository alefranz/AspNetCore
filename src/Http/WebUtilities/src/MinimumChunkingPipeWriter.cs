using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.WebUtilities
{
    internal class MinimumChunkingPipeWriter : PipeWriter
    {
        private readonly PipeWriter _pipeWriter;
        private int _uncommittedBytes = 0;

        public MinimumChunkingPipeWriter(PipeWriter pipeWriter)
        {
            _pipeWriter = pipeWriter;
        }

        public override void Advance(int bytes)
        {
            _uncommittedBytes += bytes;
            _pipeWriter.Advance(bytes);
        }

        public override void CancelPendingFlush()
        {
            _pipeWriter.CancelPendingFlush();
        }

        public override void Complete(Exception? exception = null)
        {
            _pipeWriter.Complete(exception);
        }

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            // The max size of a chunk is 4089.
            if (_uncommittedBytes > 4089)
            {
                _uncommittedBytes = 0;
                return _pipeWriter.FlushAsync(cancellationToken);
            }

            return ValueTask.FromResult(new FlushResult(false, true));
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            return _pipeWriter.GetMemory(sizeHint);
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            return _pipeWriter.GetSpan(sizeHint);
        }
    }
}
