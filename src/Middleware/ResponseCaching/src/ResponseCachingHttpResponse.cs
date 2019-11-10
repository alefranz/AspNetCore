// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.ResponseCaching
{
    public class ResponseCachingHttpResponse : HttpResponse
    {
        private readonly HttpResponse _innerHttpResponse;
        private readonly PipeWriter _pipeWriter;

        public ResponseCachingHttpResponse(HttpResponse innerHttpResponse, PipeWriter pipeWriter)
        {
            _innerHttpResponse = innerHttpResponse;
            _pipeWriter = pipeWriter;
        }

        public override HttpContext HttpContext => _innerHttpResponse.HttpContext;

        public override int StatusCode { get => _innerHttpResponse.StatusCode; set => _innerHttpResponse.StatusCode = value; }

        public override IHeaderDictionary Headers => _innerHttpResponse.Headers;

        public override Stream Body { get => _innerHttpResponse.Body; set => _innerHttpResponse.Body = value; }

        public override long? ContentLength { get => _innerHttpResponse.ContentLength; set => _innerHttpResponse.ContentLength = value; }

        public override string ContentType { get => _innerHttpResponse.ContentType; set => _innerHttpResponse.ContentType = value; }

        public override IResponseCookies Cookies => _innerHttpResponse.Cookies;

        public override bool HasStarted => _innerHttpResponse.HasStarted;

        public override void OnCompleted(Func<object, Task> callback, object state) => _innerHttpResponse.OnCompleted(callback, state);

        public override void OnStarting(Func<object, Task> callback, object state) => _innerHttpResponse.OnStarting(callback, state);

        public override void Redirect(string location, bool permanent) => _innerHttpResponse.Redirect(location, permanent);
    }
}
