using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Genesis
{
    public interface IGenesisAssetsBuilder
    {
        HashedTransaction BuildGoverningTokenRegisterTransaction();

        HashedTransaction BuildGenesisMinerTransaction();
        
        HashedTransaction BuildGenesisTokenIssue(PublicKey owner, Fixed256 supply, UInt160 asset);

        IEnumerable<HashedTransaction> IssueTransactionsToOwners(Fixed256 value, params UInt160[] assets);
    }
}