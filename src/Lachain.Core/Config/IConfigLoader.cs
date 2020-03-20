using System.Collections.Generic;

namespace Lachain.Core.Config
{
    public interface IConfigLoader
    {
        IReadOnlyDictionary<string, object> LoadConfig();
    }
}