// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.ResponseCaching
{
    internal class ResponseCachingPipeWriter : PipeWriter
    {
        private readonly PipeWriter _innerPipeWriter;
        private Memory<byte> _memory;
        private readonly List<byte[]> _segments = new List<byte[]>();

        public ResponseCachingPipeWriter(PipeWriter innerPipeWriter)
        {
            _innerPipeWriter = innerPipeWriter;
        }

        public override void Advance(int bytes)
        {
            // do we need to make sure this is not too big to avoid LOH allocations?
            var segment = _memory.Slice(0, bytes).ToArray();

            _segments.Add(segment);
            _innerPipeWriter.Advance(bytes);
        }

        public override void CancelPendingFlush()
            => _innerPipeWriter.CancelPendingFlush();

        public override void Complete(Exception exception = null)
            => _innerPipeWriter.Complete(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            => _innerPipeWriter.FlushAsync(cancellationToken);

        public override Memory<byte> GetMemory(int sizeHint = 0)
            => _memory = _innerPipeWriter.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0)
            => GetMemory(sizeHint).Span;
    }
}
