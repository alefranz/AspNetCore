using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    public class HeaderPropagationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HeaderPropagationOptions _options;
        private readonly HeaderPropagationState _state;

        public HeaderPropagationMiddleware(RequestDelegate next, IOptions<HeaderPropagationOptions> options, HeaderPropagationState state)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            _options = options.Value;

            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public Task Invoke(HttpContext context)
        {
            if (context != null)
            {
                foreach (var header in _options.Headers)
                {
                    if (!context.Request.Headers.TryGetValue(header.InputName, out var values)
                        || StringValues.IsNullOrEmpty(values))
                    {
                        if (header.DefaultValuesGenerator != null)
                        {
                            values = header.DefaultValuesGenerator();
                            if (StringValues.IsNullOrEmpty(values)) continue;
                        }
                        else if (!StringValues.IsNullOrEmpty(header.DefaultValues))
                        {
                            values = header.DefaultValues;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    _state.Headers.TryAdd(header.OutputName, values);
                }
            }

            return _next.Invoke(context);
        }
    }
}
