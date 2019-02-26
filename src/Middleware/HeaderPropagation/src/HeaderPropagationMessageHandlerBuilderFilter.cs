using System;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    internal class HeaderPropagationMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly IOptions<HeaderPropagationOptions> _options;
        private readonly HeaderPropagationState _state;

        public HeaderPropagationMessageHandlerBuilderFilter(IOptions<HeaderPropagationOptions> options, HeaderPropagationState state)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                builder.AdditionalHandlers.Add(new HeaderPropagationMessageHandler(_options, _state));
                next(builder);
            };
        }
    }
}
