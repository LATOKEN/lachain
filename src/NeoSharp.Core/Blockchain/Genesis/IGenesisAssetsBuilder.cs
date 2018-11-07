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

        IssueTransaction BuildGenesisTokenIssue(ECPoint owner, Fixed8 value, params UInt256[] assets);

        IEnumerable<IssueTransaction> IssueTransactionsToOwners(Fixed8 value, params UInt256[] assets);
        
        Witness BuildGenesisWitness();

        UInt160 BuildGenesisNextConsensusAddress();
    }
}