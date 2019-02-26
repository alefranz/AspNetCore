using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    public static class HeaderPropagationExtensions
    {
        public static IServiceCollection AddHeaderPropagation(this IServiceCollection services, Action<HeaderPropagationOptions> configure)
        {
            services.TryAddScoped<HeaderPropagationState>();
            services.Configure(configure);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, HeaderPropagationMessageHandlerBuilderFilter>());
            return services;
        }

        public static IHttpClientBuilder AddHeaderPropagation(this IHttpClientBuilder builder, Action<HeaderPropagationOptions> configure)
        {
            builder.Services.TryAddScoped<HeaderPropagationState>();
            builder.Services.Configure(configure);
            builder.Services.TryAddTransient<HeaderPropagationMessageHandler>();
 
            builder.AddHttpMessageHandler<HeaderPropagationMessageHandler>();

            return builder;
        }

        public static IApplicationBuilder UseHeaderPropagation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HeaderPropagationMiddleware>();
        }
    }
}
