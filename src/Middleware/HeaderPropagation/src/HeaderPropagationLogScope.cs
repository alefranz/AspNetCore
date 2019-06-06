using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    internal class HeaderPropagationLogScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly HeaderPropagationEntryCollection _headers;
        private readonly IDictionary<string, StringValues> _headerValues;

        private string _cachedToString;

        public HeaderPropagationLogScope(HeaderPropagationEntryCollection headers, IDictionary<string, StringValues> headerValues)
        {
            _headers = headers;
            _headerValues = headerValues;
        }

        public int Count => _headers.Count;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                var headerName = _headers[index].CapturedHeaderName;
                _headerValues.TryGetValue(headerName, out var value);
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

                    var headerName = _headers[i].CapturedHeaderName;
                    _headerValues.TryGetValue(headerName, out var value);

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
