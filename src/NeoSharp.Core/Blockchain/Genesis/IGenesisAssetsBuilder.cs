using System.Collections.Generic;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Genesis
{
    public interface IGenesisAssetsBuilder
    {
        RegisterTransaction BuildGoverningTokenRegisterTransaction();

        RegisterTransaction BuildUtilityTokenRegisterTransaction();

        MinerTransaction BuildGenesisMinerTransaction();

        IssueTransaction BuildGenesisIssueTransaction();

        IssueTransaction BuildGenesisTokenIssue(UInt256 assetHash, ECPoint owner, Fixed8 value);

        IEnumerable<IssueTransaction> IssueTransactionsToOwners(UInt256 assetHash, Fixed8 value);
        
        Witness BuildGenesisWitness();

        UInt160 BuildGenesisNextConsensusAddress();
    }
}