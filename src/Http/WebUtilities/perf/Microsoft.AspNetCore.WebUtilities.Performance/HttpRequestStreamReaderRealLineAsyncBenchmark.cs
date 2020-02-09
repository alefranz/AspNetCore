using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.AspNetCore.WebUtilities
{
    public class HttpRequestStreamReaderRealLineAsyncBenchmark
    {
        private static int Size = 1000;

        private static char[] CharData = new char[Size];

        [Params(3, 100, -1)]
        public int NewLinePosition { get; set; }
        //private HttpRequestStreamReader _reader;

        [GlobalSetup]
        public void GlobalSetup()
        {
            //var stream = CreateStream();
            //_reader = new HttpRequestStreamReader(stream, Encoding.UTF8);
            for (var i = 0; i < CharData.Length; i++)
            {
                if (i == NewLinePosition)
                {
                    CharData[i] = '\n';
                    break;
                }
                CharData[i] = 'a';
            }
        }

        [Benchmark(Baseline = true)]
        public string CharByChar()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < CharData.Length; i++)
            {
                var ch = CharData[i];
                if (ch == '\n')
                {
                    return sb.ToString();
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        [Benchmark]
        public string IndexOf()
        {
            var span = new Span<char>(CharData);
            var index = span.IndexOf('\n');
            if (index != -1)
            {
                span = span.Slice(0, index);
            }
            return span.ToString();
        }

        //[Benchmark(Baseline = true)]
        //public Task<string> ReadLineAsync()
        //{
        //    return _reader.ReadLineAsync();
        //}

        //[Benchmark]
        //public Task<string> ReadLineIndexAsync()
        //{
        //    return _reader.ReadLineIndexAsync();
        //}


        //private MemoryStream CreateStream()
        //{
        //    var stream = new MemoryStream();
        //    var writer = new StreamWriter(stream);
        //    for (var i = 0; i < CharData.Length; i++)
        //    {
        //        if (i == NewLinePosition)
        //        {
        //            writer.Write('\n');
        //            break;
        //        }
        //        writer.Write('a');
        //    }
        //    writer.Flush();
        //    stream.Position = 0;
        //    return stream;
        //}
    }

//    Method | NewLinePosition |     Mean |     Error |    StdDev |         Op/s |  Gen 0 | Allocated |
//-------------- |---------------- |---------:|----------:|----------:|-------------:|-------:|----------:|
// ReadLineAsync |              -1 | 84.53 ns | 1.7988 ns | 1.8472 ns | 11,830,578.4 | 0.0013 |     104 B |
// ReadLineAsync |               3 | 92.55 ns | 0.4452 ns | 0.3219 ns | 10,804,570.8 | 0.0012 |     104 B |
// ReadLineAsync |             100 | 83.48 ns | 0.8183 ns | 0.7655 ns | 11,979,122.7 | 0.0012 |     104 B |

}
