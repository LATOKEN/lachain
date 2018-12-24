using System.Collections.Generic;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.Genesis
{
    public interface IGenesisAssetsBuilder
    {
        Transaction BuildGoverningTokenRegisterTransaction(UInt160 owner);

        Transaction BuildPlatformTokenRegisterTransaction(UInt160 owner, string name, uint supply,
            uint precision);

        Transaction BuildGenesisMinerTransaction();
        
        Transaction BuildGenesisTokenIssue(PublicKey owner, Money supply, UInt160 asset);

        IEnumerable<Transaction> IssueTransactionsToOwners(Money value, params UInt160[] assets);
    }
}