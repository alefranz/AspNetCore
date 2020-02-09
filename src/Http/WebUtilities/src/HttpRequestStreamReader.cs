// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.WebUtilities
{
    public class HttpRequestStreamReader : TextReader
    {
        private const int DefaultBufferSize = 1024;
        private const int MinBufferSize = 128;
        private const int MaxSharedBuilderCapacity = 360; // also the max capacity used in StringBuilderCache

        private Stream _stream;
        private readonly Encoding _encoding;
        private readonly Decoder _decoder;

        private readonly ArrayPool<byte> _bytePool;
        private readonly ArrayPool<char> _charPool;

        private readonly int _byteBufferSize;
        private byte[] _byteBuffer;
        private char[] _charBuffer;

        private int _charBufferIndex;
        private int _charsRead;
        private int _bytesRead;

        private bool _isBlocked;
        private bool _disposed;

        public HttpRequestStreamReader(Stream stream, Encoding encoding)
            : this(stream, encoding, DefaultBufferSize, ArrayPool<byte>.Shared, ArrayPool<char>.Shared)
        {
        }

        public HttpRequestStreamReader(Stream stream, Encoding encoding, int bufferSize)
            : this(stream, encoding, bufferSize, ArrayPool<byte>.Shared, ArrayPool<char>.Shared)
        {
        }

        public HttpRequestStreamReader(
            Stream stream,
            Encoding encoding,
            int bufferSize,
            ArrayPool<byte> bytePool,
            ArrayPool<char> charPool)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _bytePool = bytePool ?? throw new ArgumentNullException(nameof(bytePool));
            _charPool = charPool ?? throw new ArgumentNullException(nameof(charPool));

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }
            if (!stream.CanRead)
            {
                throw new ArgumentException(Resources.HttpRequestStreamReader_StreamNotReadable, nameof(stream));
            }

            _byteBufferSize = bufferSize;

            _decoder = encoding.GetDecoder();
            _byteBuffer = _bytePool.Rent(bufferSize);

            try
            {
                var requiredLength = encoding.GetMaxCharCount(bufferSize);
                _charBuffer = _charPool.Rent(requiredLength);
            }
            catch
            {
                _bytePool.Return(_byteBuffer);

                if (_charBuffer != null)
                {
                    _charPool.Return(_charBuffer);
                }

                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;

                _bytePool.Return(_byteBuffer);
                _charPool.Return(_charBuffer);
            }

            base.Dispose(disposing);
        }

        public override int Peek()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            if (_charBufferIndex == _charsRead)
            {
                if (_isBlocked || ReadIntoBuffer() == 0)
                {
                    return -1;
                }
            }

            return _charBuffer[_charBufferIndex];
        }

        public override int Read()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            if (_charBufferIndex == _charsRead)
            {
                if (ReadIntoBuffer() == 0)
                {
                    return -1;
                }
            }

            return _charBuffer[_charBufferIndex++];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (count < 0 || index + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var span = new Span<char>(buffer, index, count);
            return Read(span);
        }

        public override int Read(Span<char> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            var count = buffer.Length;
            var charsRead = 0;
            while (count > 0)
            {
                var charsRemaining = _charsRead - _charBufferIndex;
                if (charsRemaining == 0)
                {
                    charsRemaining = ReadIntoBuffer();
                }

                if (charsRemaining == 0)
                {
                    break;  // We're at EOF
                }

                if (charsRemaining > count)
                {
                    charsRemaining = count;
                }

                var source = new ReadOnlySpan<char>(_charBuffer, _charBufferIndex, charsRemaining);
                source.CopyTo(buffer);

                _charBufferIndex += charsRemaining;

                charsRead += charsRemaining;
                count -= charsRemaining;

                if (count > 0)
                {
                    buffer = buffer.Slice(charsRemaining, count);
                }

                // If we got back fewer chars than we asked for, then it's likely the underlying stream is blocked.
                // Send the data back to the caller so they can process it.
                if (_isBlocked)
                {
                    break;
                }
            }

            return charsRead;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (count < 0 || index + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var memory = new Memory<char>(buffer, index, count);
            return ReadAsync(memory).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            if (_charBufferIndex == _charsRead && await ReadIntoBufferAsync() == 0)
            {
                return 0;
            }

            var count = buffer.Length;

            var charsRead = 0;
            while (count > 0)
            {
                // n is the characters available in _charBuffer
                var charsAvailable = _charsRead - _charBufferIndex;

                // charBuffer is empty, let's read from the stream
                if (charsAvailable == 0)
                {
                    _charsRead = 0;
                    _charBufferIndex = 0;
                    _bytesRead = 0;

                    // We loop here so that we read in enough bytes to yield at least 1 char.
                    // We break out of the loop if the stream is blocked (EOF is reached).
                    do
                    {
                        Debug.Assert(charsAvailable == 0);
                        _bytesRead = await _stream.ReadAsync(
                            _byteBuffer,
                            0,
                            _byteBufferSize);
                        if (_bytesRead == 0)  // EOF
                        {
                            _isBlocked = true;
                            break;
                        }

                        // _isBlocked == whether we read fewer bytes than we asked for.
                        _isBlocked = (_bytesRead < _byteBufferSize);

                        Debug.Assert(charsAvailable == 0);

                        _charBufferIndex = 0;
                        charsAvailable = _decoder.GetChars(
                            _byteBuffer,
                            0,
                            _bytesRead,
                            _charBuffer,
                            0);

                        Debug.Assert(charsAvailable > 0);

                        _charsRead += charsAvailable; // Number of chars in StreamReader's buffer.
                    }
                    while (charsAvailable == 0);

                    if (charsAvailable == 0)
                    {
                        break; // We're at EOF
                    }
                }

                // Got more chars in charBuffer than the user requested
                if (charsAvailable > count)
                {
                    charsAvailable = count;
                }

                var source = new Memory<char>(_charBuffer, _charBufferIndex, charsAvailable);
                source.CopyTo(buffer);

                if (charsAvailable < count)
                {
                    // update the buffer to the remaining portion
                    buffer = buffer.Slice(charsAvailable);
                }

                _charBufferIndex += charsAvailable;

                charsRead += charsAvailable;
                count -= charsAvailable;

                // This function shouldn't block for an indefinite amount of time,
                // or reading from a network stream won't work right.  If we got
                // fewer bytes than we requested, then we want to break right here.
                if (_isBlocked)
                {
                    break;
                }
            }

            return charsRead;
        }

        public override async Task<string> ReadLineAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (_charBufferIndex == _charsRead)
                {
                    if (await ReadIntoBufferAsync() == 0)
                    {
                        break;  // reached EOF, we need to return null if we were at EOF from the beginning
                    }
                }

                var ch = _charBuffer[_charBufferIndex++];

                if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r')
                    {
                        if (_charBufferIndex == _charsRead)
                        {
                            if (await ReadIntoBufferAsync() == 0)
                            {
                                return sb.ToString();  // reached EOF
                            }
                        }

                        if (_charBuffer[_charBufferIndex] == '\n')
                        {
                            _charBufferIndex++;  // consume the \n character
                        }
                    }

                    return sb.ToString();
                }
                sb.Append(ch);
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return null;
        }

        public async Task<string> ReadLineNewAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (_charBufferIndex == _charsRead)
                {
                    if (await ReadIntoBufferAsync() == 0)
                    {
                        break;  // reached EOF, we need to return null if we were at EOF from the beginning
                    }
                }

                var ch = _charBuffer[_charBufferIndex++];
                
                if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r')
                    {
                        if (_charBufferIndex == _charsRead)
                        {
                            if (await ReadIntoBufferAsync() == 0)
                            {
                                return sb.ToString();  // reached EOF
                            }
                        }

                        if (_charBuffer[_charBufferIndex] == '\n')
                        {
                            _charBufferIndex++;  // consume the \n character
                        }
                    }

                    return sb.ToString();
                }
                sb.Append(ch);
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return null;
        }

        // true if last is \r
        private bool UpdateStringBuilder(StringBuilder sb, bool hadCarriageReturn)
        {
            if (hadCarriageReturn && _charBuffer[_charBufferIndex] == '\n')
            {
                _charBufferIndex++;
                if (_charBufferIndex == _charsRead) return false;
            }

            var span = new Span<char>(_charBuffer, _charBufferIndex, _charsRead - _charBufferIndex);


            // \r
            // \n
            // \r\n

            var index = span.IndexOf('\r');
            if (index == -1)
            {
                index = span.IndexOf('\n');
                if (index == -1)
                {
                    // no newline so far
                    sb.Append(span);
                    _charBufferIndex = _charsRead;
                }
                else
                {
                    // \n
                    sb.Append(span.Slice(0, index));
                    _charBufferIndex += index + 1;
                }
            }
            else
            {
                // \r...
                if (index == span.Length - 1) // at the end
                {
                    // reached EOF
                    // not perfectly accurate, we should probably read more into the buffer,
                    // but we can't as we haven't consumed anything yet

                    // \rEOF
                    sb.Append(span.Slice(0, index));
                    _charBufferIndex += index + 1;
                    return true;
                }

                var next = span[index + 1];
                if (next == '\n')
                {
                    // \r\n
                    sb.Append(span.Slice(0, index));
                    _charBufferIndex += index + 2;  // consume also \n

                }
                else
                {
                    // \r
                    sb.Append(span.Slice(0, index));
                    _charBufferIndex += index + 1;
                }
            }
            return false;
        }

        public async Task<string> ReadLineIndexAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpRequestStreamReader));
            }

            StringBuilder sb = new StringBuilder();
            bool hadCarriageReturn = false;
            while (true)
            {
                if (_charBufferIndex == _charsRead)
                {
                    if (await ReadIntoBufferAsync() == 0)
                    {
                        break;  // reached EOF, we need to return null if we were at EOF from the beginning
                    }
                }

                hadCarriageReturn = UpdateStringBuilder(sb, hadCarriageReturn);
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return null;
        }

        // Reads a line. A line is defined as a sequence of characters followed by
        // a carriage return ('\r'), a line feed ('\n'), or a carriage return
        // immediately followed by a line feed. The resulting string does not
        // contain the terminating carriage return and/or line feed. The returned
        // value is null if the end of the input stream has been reached.
        //
        public override string ReadLine()
        {
            StringBuilder sb = null;
            int index;

            while (true)
            {
                if (_charBufferIndex == _charsRead)
                {
                    if (ReadIntoBuffer() == 0)
                    {
                        break;
                    }
                }

                var span = new Span<char>(_charBuffer, _charBufferIndex, _charsRead - _charBufferIndex);

                if ((index = span.IndexOf('\r')) != -1)
                {
                    span = span.Slice(0, index);
                    _charBufferIndex += index;

                    if (_charBufferIndex < _charsRead)
                    {
                        // consume following \n
                        if (_charBuffer[_charBufferIndex] == '\n')
                        {
                            _charBufferIndex++;
                        }

                        if (sb != null)
                        {
                            sb.Append(span);
                            break;
                        }

                        // perf: if the new line is found in first pass, we skip the StringBuilder
                        return span.Length > 0 ? span.ToString() : null;
                    }

                    // we where at end of buffer, we need to consume the buffer so we can read more to check for \n
                    sb ??= new StringBuilder();
                    sb.Append(span);
                    if (ReadIntoBuffer() != 0)
                    {
                        if (_charBuffer[_charBufferIndex] == '\n')
                        {
                            _charBufferIndex++;
                        }
                    }
                    break;
                }

                if ((index = span.IndexOf('\n')) != -1)
                {
                    span = span.Slice(0, index);
                    _charBufferIndex += index;

                    if (sb != null)
                    {
                        sb.Append(span);
                        break;
                    }

                    // perf: if the new line is found in first pass, we skip the StringBuilder
                    return span.Length > 0 ? span.ToString() : null;
                }

                sb ??= new StringBuilder();
                sb.Append(span);
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        private int ReadIntoBuffer()
        {
            _charsRead = 0;
            _charBufferIndex = 0;
            _bytesRead = 0;

            do
            {
                _bytesRead = _stream.Read(_byteBuffer, 0, _byteBufferSize);
                if (_bytesRead == 0)  // We're at EOF
                {
                    return _charsRead;
                }

                _isBlocked = (_bytesRead < _byteBufferSize);
                _charsRead += _decoder.GetChars(
                    _byteBuffer,
                    0,
                    _bytesRead,
                    _charBuffer,
                    _charsRead);
            }
            while (_charsRead == 0);

            return _charsRead;
        }

        private async Task<int> ReadIntoBufferAsync()
        {
            _charsRead = 0;
            _charBufferIndex = 0;
            _bytesRead = 0;

            do
            {
                _bytesRead = await _stream.ReadAsync(
                    _byteBuffer,
                    0,
                    _byteBufferSize).ConfigureAwait(false);
                if (_bytesRead == 0)
                {
                    // We're at EOF
                    return _charsRead;
                }

                // _isBlocked == whether we read fewer bytes than we asked for.
                _isBlocked = (_bytesRead < _byteBufferSize);

                _charsRead += _decoder.GetChars(
                    _byteBuffer,
                    0,
                    _bytesRead,
                    _charBuffer,
                    _charsRead);
            }
            while (_charsRead == 0);

            return _charsRead;
        }
    }
}
