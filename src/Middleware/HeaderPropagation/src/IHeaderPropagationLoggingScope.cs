using System.Collections.Generic;

namespace Microsoft.AspNetCore.HeaderPropagation
{
    public interface IHeaderPropagationLoggingScope : IReadOnlyList<KeyValuePair<string, object>>
    {
    }
}
