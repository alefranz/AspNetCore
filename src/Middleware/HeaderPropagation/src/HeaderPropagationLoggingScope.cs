using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    public class HeaderPropagationLoggingScope : IHeaderPropagationLoggingScope
    {
        private readonly List<string> _headerNames;
        private readonly HeaderPropagationValues _values;
        private string _cachedToString;

        public HeaderPropagationLoggingScope(IOptions<HeaderPropagationOptions> options, HeaderPropagationValues values)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var headers = options.Value.Headers;
            var uniqueHeaderNames = new HashSet<string>();
            // Perf: not using directly the HashSet so we can iterate without allocating an enumerator.
            _headerNames = new List<string>();

            // Perf: avoiding foreach since we don't define a struct-enumerator.
            for (var i=0; i < headers.Count; i++)
            {
                var headerName = headers[i].CapturedHeaderName;
                if (uniqueHeaderNames.Add(headerName))
                {
                    _headerNames.Add(headerName);
                }
            }

            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public int Count => _headerNames.Count;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                var headerName = _headerNames[index];
                _values.Headers.TryGetValue(headerName, out var value);
                return new KeyValuePair<string, object>(headerName, value);
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            if (_cachedToString == null)
            {
                var sb = new StringBuilder();

                for (int i = 0; i < Count; i++)
                {
                    if (i > 0) sb.Append(' ');

                    var headerName = _headerNames[i];
                    _values.Headers.TryGetValue(headerName, out var value);

                    sb.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}",
                        headerName, value.ToString()));
                }

                _cachedToString = sb.ToString();
            }

            return _cachedToString;
        }
    }
}
