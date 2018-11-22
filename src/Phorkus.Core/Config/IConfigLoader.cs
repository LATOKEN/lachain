using System.Collections.Generic;

namespace Phorkus.Core.Config
{
    public interface IConfigLoader
    {
        IReadOnlyDictionary<string, object> LoadConfig();
    }
}