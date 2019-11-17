using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.ResponseCaching
{
    public class ResponseCompressionBodyFeature : IHttpResponseBodyFeature
    {

        public Stream Stream => throw new NotImplementedException();

        public PipeWriter Writer => throw new NotImplementedException();

        public Task CompleteAsync()
        {
            throw new NotImplementedException();
        }

        public void DisableBuffering()
        {
            throw new NotImplementedException();
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
