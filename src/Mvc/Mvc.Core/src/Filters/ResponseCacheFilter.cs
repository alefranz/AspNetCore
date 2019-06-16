// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// An <see cref="IActionFilter"/> which sets the appropriate headers related to response caching.
    /// </summary>
    internal class ResponseCacheFilter : IActionFilter, IResponseCacheFilter
    {
        private readonly ResponseCacheFilterExecutor _executor;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of <see cref="ResponseCacheFilter"/>
        /// </summary>
        /// <param name="cacheProfile">The profile which contains the settings for
        /// <see cref="ResponseCacheFilter"/>.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public ResponseCacheFilter(CacheProfile cacheProfile, ILoggerFactory loggerFactory)
        {
            _executor = new ResponseCacheFilterExecutor(cacheProfile);
            _logger = loggerFactory.CreateLogger(GetType());
        }

        /// <summary>
        /// Gets or sets the duration in seconds for which the response is cached.
        /// This is a required parameter.
        /// This sets "max-age" in "Cache-control" header.
        /// </summary>
        public int Duration
        {
            get => _executor.Duration;
            set => _executor.Duration = value;
        }

        /// <summary>
        /// Gets or sets the location where the data from a particular URL must be cached.
        /// </summary>
        public ResponseCacheLocation Location
        {
            get => _executor.Location;
            set => _executor.Location = value;
        }

        /// <summary>
        /// Gets or sets the value which determines whether the data should be stored or not.
        /// When set to <see langword="true"/>, it sets "Cache-control" header to "no-store".
        /// Ignores the "Location" parameter for values other than "None".
        /// Ignores the "duration" parameter.
        /// </summary>
        public bool NoStore
        {
            get => _executor.NoStore;
            set => _executor.NoStore = value;
        }

        /// <summary>
        /// Gets or sets the value for the Vary response header.
        /// </summary>
        public string VaryByHeader
        {
            get => _executor.VaryByHeader;
            set => _executor.VaryByHeader = value;
        }

        /// <summary>
        /// Gets or sets the query keys to vary by.
        /// </summary>
        /// <remarks>
        /// <see cref="VaryByQueryKeys"/> requires the response cache middleware.
        /// </remarks>
        public string[] VaryByQueryKeys
        {
            get => _executor.VaryByQueryKeys;
            set => _executor.VaryByQueryKeys = value;
        }

        /// <summary>
        /// Gets or sets the status codes for which apply the cache headers.
        /// </summary>
        /// <remarks>
        /// When not specified or empty, the headers are always added.
        /// </remarks>
        public int[] ApplyForStatusCodes
        {
            get; set;
        }

        /// <inheritdoc />
        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        /// <inheritdoc />
        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!IsApplicable(context))
            {
                return;
            }

            // If there are more filters which can override the values written by this filter,
            // then skip execution of this filter.
            var effectivePolicy = FindEffectivePolicy(context.Filters, context);
            if (effectivePolicy != null && effectivePolicy != this)
            {
                _logger.NotMostEffectiveFilter(GetType(), effectivePolicy.GetType(), typeof(IResponseCacheFilter));
                return;
            }

            _executor.Execute(context);
        }

        private bool IsApplicable(ActionExecutedContext context)
        {
            if (ApplyForStatusCodes == null || ApplyForStatusCodes.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < ApplyForStatusCodes.Length; i++)
            {
                if (context.HttpContext.Response.StatusCode == ApplyForStatusCodes[i])
                {
                    return true;
                }
            }
            return false;
        }

        private IResponseCacheFilter FindEffectivePolicy(IList<IFilterMetadata> filters, ActionExecutedContext context)
        {
            // The most specific policy is the one closest to the action (nearest the end of the list).
            for (var i = filters.Count - 1; i >= 0; i--)
            {
                var filter = filters[i];
                if (filter is IResponseCacheFilter genericResponseCacheFilter)
                {
                    if (genericResponseCacheFilter is ResponseCacheFilter responseCacheFilter)
                    {
                        // Find the most specific filter which is applicable for the current context
                        if (responseCacheFilter.IsApplicable(context))
                        {
                            return responseCacheFilter;
                        }
                    }
                    else
                    {
                        // if the most specific IResponseCacheFilter is not a ResponseCacheFilter, it is the most effective
                        return genericResponseCacheFilter;
                    }
                }
            }

            return null;
        }
    }
}
