using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    public class HeaderPropagationState
    {
        public Dictionary<string, StringValues> Headers { get; } = new Dictionary<string, StringValues>();
    }
}
