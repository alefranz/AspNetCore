using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.ResponseCaching
{
    class ResponseCachingBodyControl : IHttpBodyControlFeature
    {
        public bool AllowSynchronousIO { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
