using System.Diagnostics;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;

namespace Microsoft.AspNetCore.WebUtilities
{
    [SimpleJob]
    public class HttpRequestStreamReaderReadLineBenchmark
    {
        private MemoryStream _stream;
        private HttpRequestStreamReader _reader;

        [Params(2, 50_000)]
        public int Length { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            var data = new char[Length];

            data[Length - 2] = '\r';
            data[Length - 1] = '\n';

            _stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            _reader = new HttpRequestStreamReader(_stream, Encoding.UTF8);
        }

        [Benchmark]
        public string ReadLine()
        {
            var result = _reader.ReadLine();
            Debug.Assert(result.Length == Length - 2);
            return result;
        }

        [Benchmark]
        public string BaseReadLine()
        {
            var result = _reader.BaseReadLine();
            Debug.Assert(result.Length == Length - 2);
            return result;
        }
    }
}
