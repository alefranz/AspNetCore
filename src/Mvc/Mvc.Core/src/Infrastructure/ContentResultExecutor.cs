﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Infrastructure
{
    public class ContentResultExecutor : IActionResultExecutor<ContentResult>
    {
        private const string DefaultContentType = "text/plain; charset=utf-8";
        private readonly ILogger<ContentResultExecutor> _logger;
        private readonly IHttpResponseWriterFactory _httpResponsePipeWriterFactory;

        public ContentResultExecutor(ILogger<ContentResultExecutor> logger, IHttpResponseWriterFactory httpResponseStreamWriterFactory)
        {
            _logger = logger;
            _httpResponsePipeWriterFactory = httpResponseStreamWriterFactory;
        }

        /// <inheritdoc />
        public virtual async Task ExecuteAsync(ActionContext context, ContentResult result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var response = context.HttpContext.Response;

            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                result.ContentType,
                response.ContentType,
                DefaultContentType,
                out var resolvedContentType,
                out var resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (result.StatusCode != null)
            {
                response.StatusCode = result.StatusCode.Value;
            }

            _logger.ContentResultExecuting(resolvedContentType);

            if (result.Content != null)
            {
                response.ContentLength = resolvedContentTypeEncoding.GetByteCount(result.Content);

                await using (var textWriter = _httpResponsePipeWriterFactory.CreateWriter(response.BodyWriter, resolvedContentTypeEncoding))
                {
                    await textWriter.WriteAsync(result.Content);

                    // Flushing the HttpResponseStreamWriter does not flush the underlying stream. This just flushes
                    // the buffered text in the writer.
                    // We do this rather than letting dispose handle it because dispose would call Write and we want
                    // to call WriteAsync.
                    await textWriter.FlushAsync();
                }
            }
        }
    }
}
