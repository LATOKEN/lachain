using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Repositories
{
    public interface IStateRepository
    {
        Task<IEnumerable<CoinReference>> GetUnspent(UInt160 address);
    }
}