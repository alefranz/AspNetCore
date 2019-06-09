// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.HeaderPropagation.Tests
{
    public class HeaderPropagationIntegrationTest
    {
        [Fact]
        public async Task HeaderPropagation_WithoutMiddleware_Throws()
        {
            // Arrange
            Exception captured = null;

            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddHttpClient("test").AddHeaderPropagation();
                    services.AddHeaderPropagation(options =>
                    {
                        options.Headers.Add("X-TraceId");
                    });
                })
                .Configure(app =>
                {
                    // note: no header propagation middleware

                    app.Run(async context =>
                    {
                        try
                        {
                            var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("test");
                            await client.GetAsync("http://localhost/"); // will throw
                        }
                        catch (Exception ex)
                        {
                            captured = ex;
                        }
                    });
                });

            var server = new TestServer(builder);
            var client = server.CreateClient();

            var request = new HttpRequestMessage();

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.IsType<InvalidOperationException>(captured);
            Assert.Equal(
                "The HeaderPropagationValues.Headers property has not been initialized. Register the header propagation middleware " +
                "by adding 'app.UseHeaderPropagation() in the 'Configure(...)' method.",
                captured.Message);
        }

        [Fact]
        public async Task HeaderInRequest_AddCorrectValue()
        {
            // Arrange
            var handler = new SimpleHandler();
            var builder = CreateBuilder(c =>
                c.Headers.Add("in", "out"),
                handler);
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var request = new HttpRequestMessage();
            request.Headers.Add("in", "test");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(handler.Headers.Contains("out"));
            Assert.Equal(new[] { "test" }, handler.Headers.GetValues("out"));
        }

        [Fact]
        public async Task MultipleHeaders_HeadersInRequest_AddAllHeaders()
        {
            // Arrange
            var handler = new SimpleHandler();
            var builder = CreateBuilder(c =>
                {
                    c.Headers.Add("first");
                    c.Headers.Add("second");
                },
                handler);
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var request = new HttpRequestMessage();
            request.Headers.Add("first", "value");
            request.Headers.Add("second", "other");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(handler.Headers.Contains("first"));
            Assert.Equal(new[] { "value" }, handler.Headers.GetValues("first"));
            Assert.True(handler.Headers.Contains("second"));
            Assert.Equal(new[] { "other" }, handler.Headers.GetValues("second"));
        }

        [Fact]
        public void Builder_UseHeaderPropagation_Without_AddHeaderPropagation_Throws()
        {
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseHeaderPropagation();
                });

            var exception = Assert.Throws<InvalidOperationException>(() => new TestServer(builder));
            Assert.Equal(
                "Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddHeaderPropagation' inside the call to 'ConfigureServices(...)' in the application startup code.",
                exception.Message);
        }

        [Fact]
        public async Task HeaderInRequest_OverrideHeaderPerClient_AddCorrectValue()
        {
            // Arrange
            var handler = new SimpleHandler();
            var builder = CreateBuilder(
                c => c.Headers.Add("in", "out"),
                handler,
                c => c.Headers.Add("out", "different"));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var request = new HttpRequestMessage();
            request.Headers.Add("in", "test");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(handler.Headers.Contains("different"));
            Assert.Equal(new[] { "test" }, handler.Headers.GetValues("different"));
        }

        [Fact]
        public async Task HeaderInRequest_IncludeInLoggerScope_AddScopeToLogger()
        {
            // Arrange
            var handler = new SimpleHandler();
            var loggingProvider = new LoggerProvider();
            var builder = CreateBuilder(c =>
                {
                    c.IncludeInLoggerScope = true;
                    c.Headers.Add("foo");
                },
                handler)
                .ConfigureLogging((_, logging) => logging.AddProvider(loggingProvider));
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var request = new HttpRequestMessage();
            request.Headers.Add("foo", "bar");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(handler.Headers.Contains("foo"));
            Assert.Equal(new[] { "bar" }, handler.Headers.GetValues("foo"));
            Assert.Single(loggingProvider.Scopes);
            var scope = loggingProvider.Scopes[0];
            Assert.Single(scope);
            Assert.IsType<HeaderPropagationLoggerScope>(scope);
            var entry = scope[0];
            Assert.Equal("foo", entry.Key);
            Assert.Equal("bar", (StringValues)entry.Value);
        }

        private IWebHostBuilder CreateBuilder(Action<HeaderPropagationOptions> configure, HttpMessageHandler primaryHandler, Action<HeaderPropagationMessageHandlerOptions> configureClient = null)
        {
            return new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseHeaderPropagation();
                    app.UseMiddleware<SimpleMiddleware>();
                })
                .ConfigureServices(services =>
                {
                    services.AddHeaderPropagation(configure);
                    var client = services.AddHttpClient("example.com", c => c.BaseAddress = new Uri("http://example.com"))
                        .ConfigureHttpMessageHandlerBuilder(b =>
                        {
                            b.PrimaryHandler = primaryHandler;
                        });

                    if (configureClient != null)
                    {
                        client.AddHeaderPropagation(configureClient);
                    }
                    else
                    {
                        client.AddHeaderPropagation();
                    }
                });
        }

        private class SimpleHandler : DelegatingHandler
        {
            public HttpHeaders Headers { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Headers = request.Headers;
                return Task.FromResult(new HttpResponseMessage());
            }
        }

        private class SimpleMiddleware
        {
            private readonly IHttpClientFactory _httpClientFactory;

            public SimpleMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory)
            {
                _httpClientFactory = httpClientFactory;
            }

            public Task InvokeAsync(HttpContext _)
            {
                var client = _httpClientFactory.CreateClient("example.com");
                return client.GetAsync("");
            }
        }

        private class LoggerProvider : ILoggerProvider, ILogger
        {
            public List<HeaderPropagationLoggerScope> Scopes { get; } = new List<HeaderPropagationLoggerScope>();

            public bool IsEnabled(LogLevel logLevel) => true;

            public ILogger CreateLogger(string name) => this;

            public IDisposable BeginScope<TState>(TState state)
            {
                if (state is HeaderPropagationLoggerScope scope)
                {
                    Scopes.Add(scope);
                }
                return this;
            }
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
