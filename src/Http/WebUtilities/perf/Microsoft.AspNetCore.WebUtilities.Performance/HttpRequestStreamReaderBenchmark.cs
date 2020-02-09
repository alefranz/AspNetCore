using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;

namespace Microsoft.AspNetCore.WebUtilities
{
    [SimpleJob]
    public class HttpRequestStreamReaderBenchmark
    {
        [Params(10, 1_000, 1_000_000)]
        public int Size { get; set; }

        [Params(0, 5, 50, 345_678)]
        public int Index { get; set; }

        private char[] Source;
        private char[] Destination;

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (Index >= Size) throw new ArgumentOutOfRangeException(nameof(Index));

            Source = new char[Size];
            Destination = new char[Size - Index];
        }

        [Benchmark]
        public void SpanCopy()
        {
            var source = new ReadOnlySpan<char>(Source, Index, Size - Index);
            var destination = new Span<char>(Destination);

            source.CopyTo(destination);
        }

        [Benchmark]
        public void BlockCopy()
        {
            Buffer.BlockCopy(
                Source,
                Index * 2,
                Destination,
                0,
                (Size - Index) * 2);
        }
    }
}
