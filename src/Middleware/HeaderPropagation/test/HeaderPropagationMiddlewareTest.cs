using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.HeaderPropagation.Tests
{
    public class HeaderPropagationMiddlewareTest
    {
        public HeaderPropagationMiddlewareTest()
        {
            Configuration = new HeaderPropagationEntry
            {
                InputName = "in",
                OutputName = "out",
            };
            Context = new DefaultHttpContext();
            Next = ctx => Task.CompletedTask;
            Options = new HeaderPropagationOptions { Headers = new List<HeaderPropagationEntry> { Configuration } };
            State = new HeaderPropagationState();
            Middleware = new HeaderPropagationMiddleware(Next,
                new OptionsWrapper<HeaderPropagationOptions>(Options),
                State);
        }

        private HeaderPropagationEntry Configuration { get; }
        public DefaultHttpContext Context { get; set; }
        public RequestDelegate Next { get; set; }
        public HeaderPropagationOptions Options { get; set; }
        public HeaderPropagationState State { get; set; }
        public HeaderPropagationMiddleware Middleware { get; set; }
        

        [Fact]
        public async Task HeaderInRequest_AddCorrectValue()
        {
            // Arrange
            Context.Request.Headers.Add("in", "test");

            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.Contains("out", State.Headers.Keys);
            Assert.Equal(new[] { "test" }, State.Headers["out"]);
        }

        [Fact]
        public async Task NoHeaderInRequest_DoesNotAddIt()
        {
            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.Empty(State.Headers);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task HeaderEmptyInRequest_DoesNotAddIt(string headerValue)
        {
            // Arrange
            Context.Request.Headers.Add("in", headerValue);

            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.DoesNotContain("out", State.Headers.Keys);
        }

        [Theory]
        [InlineData(new[] {"default"}, new[] {"default"})]
        [InlineData(new[] {"default", "other"}, new[] {"default", "other"})]
        public async Task NoHeaderInRequest_AddsDefaultValue(string[] defaultValues,
            string[] expectedValues)
        {
            // Arrange
            Configuration.DefaultValues = defaultValues;

            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.Contains("out", State.Headers.Keys);
            Assert.Equal(expectedValues, State.Headers["out"]);
        }

        [Theory]
        [InlineData(new[] {"default"}, new[] {"default"})]
        [InlineData(new[] {"default", "other"}, new[] {"default", "other"})]
        public async Task NoHeaderInRequest_UsesDefaultValuesGenerator(string[] defaultValues,
            string[] expectedValues)
        {
            // Arrange
            HttpContext receivedContext = null;
            Configuration.DefaultValues = "no";
            Configuration.DefaultValuesGenerator = ctx =>
            {
                receivedContext = ctx;
                return defaultValues;
            };

            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.Contains("out", State.Headers.Keys);
            Assert.Equal(expectedValues, State.Headers["out"]);
            Assert.Same(Context, receivedContext);
        }

        [Fact]
        public async Task NoHeaderInRequest_EmptyDefaultValuesGenerated_DoesNotAddit()
        {
            // Arrange
            Configuration.DefaultValuesGenerator = ctx => StringValues.Empty;

            // Act
            await Middleware.Invoke(Context);

            // Assert
            Assert.DoesNotContain("out", State.Headers.Keys);
        }
    }
}
