using Lachain.Consensus;

namespace Lachain.Core.Vault
{
    public interface IKeyGenManager
    {
        bool RescanBlockChainForKeys(IPublicConsensusKeySet publicKeysToSearch);
    }
}