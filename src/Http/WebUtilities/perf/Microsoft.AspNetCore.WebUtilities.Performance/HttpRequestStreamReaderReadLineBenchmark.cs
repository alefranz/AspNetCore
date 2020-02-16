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

        [Params(2, 1000, 1050)]  // Default buffer length is 1024
        public int Length { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            var data = new char[Length];

            data[Length - 2] = '\r';
            data[Length - 1] = '\n';

            _stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        }

        [Benchmark]
        public string ReadLine()
        {
            var reader = CreateReader();
            var result = reader.ReadLine();
            Debug.Assert(result.Length == Length - 2);
            return result;
        }

        [Benchmark]
        public string BaseReadLine()
        {
            var reader = CreateReader();
            var result = reader.BaseReadLine();
            Debug.Assert(result.Length == Length - 2);
            return result;
        }

        [Benchmark]
        public HttpRequestStreamReader CreateReader()
        {
            _stream.Seek(0, SeekOrigin.Begin);
            return new HttpRequestStreamReader(_stream, Encoding.UTF8);
        }
    }
}
