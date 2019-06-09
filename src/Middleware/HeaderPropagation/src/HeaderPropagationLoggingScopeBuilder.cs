// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    /// <summary>
    /// A builder for generating the logging scope for the <see cref="HeaderPropagationMiddleware"/>.
    /// </summary>
    public class HeaderPropagationLoggingScopeBuilder
    {
        private readonly List<string> _headerNames = new List<string>();
        private readonly HeaderPropagationValues _values;

        /// <summary>
        /// Creates a new instance of the <see cref="HeaderPropagationLoggingScopeBuilder"/>.
        /// </summary>
        /// <param name="options">The options that define which headers are propagated.</param>
        /// <param name="values">The values of the headers to be propagated populated by the
        /// <see cref="HeaderPropagationMiddleware"/>.</param>
        public HeaderPropagationLoggingScopeBuilder(IOptions<HeaderPropagationOptions> options, HeaderPropagationValues values)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            var headers = options.Value.Headers;

            _values = values ?? throw new ArgumentNullException(nameof(values));

            var uniqueHeaderNames = new HashSet<string>();

            // Perf: not using directly the HashSet so we can iterate without allocating an enumerator
            // and avoiding foreach since we don't define a struct-enumerator.
            for (var i = 0; i < headers.Count; i++)
            {
                var headerName = headers[i].CapturedHeaderName;
                if (uniqueHeaderNames.Add(headerName))
                {
                    _headerNames.Add(headerName);
                }
            }
        }

        internal HeaderPropagationLoggingScope Build() => new HeaderPropagationLoggingScope(_headerNames, _values.Headers);
    }
}
