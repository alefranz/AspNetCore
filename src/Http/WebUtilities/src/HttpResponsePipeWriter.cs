// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.WebUtilities
{
    /// <summary>
    /// Writes to the <see cref="PipeWriter"/> using the supplied <see cref="Encoding"/>.
    /// It does not write the BOM and also does not close the stream.
    /// </summary>
    public class HttpResponsePipeWriter : TextWriter
    {
        private readonly Encoder _encoder;
        private readonly PipeWriter _writer;

        public override Encoding Encoding { get; }

        private bool _disposed;

        public HttpResponsePipeWriter(
            PipeWriter writer,
            Encoding encoding)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _encoder = encoding.GetEncoder();
        }

        public override void Write(ReadOnlySpan<char> value)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpResponseStreamWriter));
            }

            var length = _encoder.GetByteCount(value, false);
            var buffer = _writer.GetSpan(length);
            _encoder.GetBytes(value, buffer, false);
            _writer.Advance(length);
        }

        public override Task WriteAsync(char[] values, int index, int count)
        {
            if (_disposed)
            {
                return GetObjectDisposedTask();
            }

            if (values == null || count == 0)
            {
                return Task.CompletedTask;
            }

            var length = _encoder.GetByteCount(values, false);
            var buffer = _writer.GetSpan(length);
            _encoder.GetBytes(values, buffer, false);
            _writer.Advance(length);

            return Task.CompletedTask;
        }

        public override Task WriteAsync(string value)
        {
            if (_disposed)
            {
                return GetObjectDisposedTask();
            }

            var length = _encoder.GetByteCount(value, false);
            var buffer = _writer.GetSpan(length);
            _encoder.GetBytes(value, buffer, false);
            _writer.Advance(length);

            return Task.CompletedTask;
        }

        public override Task FlushAsync()
        {
            if (_disposed)
            {
                return GetObjectDisposedTask();
            }

            return FlushInternalAsync().AsTask();
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await FlushInternalAsync();
            }

            await base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                FlushEncoder();
                _writer.Complete();
                // flush??
            }

            base.Dispose(disposing);
        }

        private async ValueTask FlushInternalAsync()
        {
            FlushEncoder();
            // await _writer.FlushAsync();
            await _writer.CompleteAsync();
        }

        private void FlushEncoder()
        {
            // flush encoder
            var empty = new ReadOnlySpan<char>();
            var length = _encoder.GetByteCount(empty, true);
            if (length > 0)
            {
                var span = _writer.GetSpan(length);
                _encoder.GetBytes(empty, span, true);
                _writer.Advance(length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task GetObjectDisposedTask()
        {
            return Task.FromException(new ObjectDisposedException(nameof(HttpResponsePipeWriter)));
        }
    }
}
