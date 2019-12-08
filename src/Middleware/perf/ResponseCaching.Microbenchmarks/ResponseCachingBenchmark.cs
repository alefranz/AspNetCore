// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.WebSockets.Microbenchmarks
{
    public class ResponseCachingBenchmark
    {
        private static readonly string _cacheControl = $"{CacheControlHeaderValue.PublicString}, {CacheControlHeaderValue.MaxAgeString}={int.MaxValue}";

        private ResponseCachingMiddleware _middleware;
        //private readonly MemoryStream _memory;
        private readonly byte[] _data = new byte[100 * 1024];

        [Params(10, 1024, 100*1024)]
        public int Size { get; set; }

        public IEnumerable<string> CacheControlValues => new[] {
            $"{CacheControlHeaderValue.PublicString}, {CacheControlHeaderValue.MaxAgeString}={int.MaxValue}",
            $"{CacheControlHeaderValue.PublicString}, {CacheControlHeaderValue.MaxAgeString}={int.MaxValue}"
        };

        [ParamsSource(nameof(CacheControlValues))]
        public string CacheControl { get; set; }

        public ResponseCachingBenchmark()
        {
            //_memory = new MemoryStream();
        }

        [GlobalSetup]
        public void Setup()
        {
            _middleware = new ResponseCachingMiddleware(
                    async context => {
                        context.Response.Headers[HeaderNames.CacheControl] = _cacheControl;
                        await context.Response.BodyWriter.WriteAsync(_data.AsMemory(0, Size));
                    },
                    Options.Create(new ResponseCachingOptions
                    {
                        SizeLimit = int.MaxValue // ~2GB
                    }),
                    NullLoggerFactory.Instance,
                    new DefaultObjectPoolProvider()
                );

            // no need to actually cache as there is a warm-up fase
        }

        [Benchmark]
        public async Task Cache()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/a";

            // don't serve from cache but store result
            context.Request.Headers[HeaderNames.CacheControl] = CacheControlHeaderValue.NoCacheString;

            await _middleware.Invoke(context);
        }

        [Benchmark]
        public async Task ServeFromCache()
        {
            //_memory.Seek(0, SeekOrigin.Begin);
            var context = new DefaultHttpContext();
            //context.Response.Body = _memory;
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/b";

            await _middleware.Invoke(context);

            context.Response.BodyWriter.Complete();
        }
    }
}
