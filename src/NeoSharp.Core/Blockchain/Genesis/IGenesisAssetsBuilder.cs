using System.Collections.Generic;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models.Transcations;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Genesis
{
    public interface IGenesisAssetsBuilder
    {
        RegisterTransaction BuildGoverningTokenRegisterTransaction();

        MinerTransaction BuildGenesisMinerTransaction();

        IssueTransaction BuildGenesisTokenIssue(PublicKey owner, UInt256 value, UInt160 asset);

        IEnumerable<IssueTransaction> IssueTransactionsToOwners(UInt256 value, params UInt160[] assets);

        UInt160 BuildGenesisNextConsensusAddress();
    }
}