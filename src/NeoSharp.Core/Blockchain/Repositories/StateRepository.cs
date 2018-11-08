using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Core.Persistence;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Repositories
{
    public class StateRepository : IStateRepository
    {
        private readonly IRepository _repository;

        public StateRepository(IRepository repository)
        {
            _repository = repository;
        }
        
        public async Task<IEnumerable<CoinReference>> GetUnspent(UInt160 address)
        {
            /* TODO: "blockchain indexer should be implemented" */
            var totalHeight = await _repository.GetTotalBlockHeight();
            var outputs = new List<CoinReference>();
            for (uint i = 0; i <= totalHeight; i++)
            {
                var blockHash = await _repository.GetBlockHashFromHeight(i);
                if (blockHash is null)
                    continue;
                var block = await _repository.GetBlockHeader(blockHash);
                if (block is null)
                    continue;
                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = await _repository.GetTransaction(txHash);
                    if (tx is null || tx.Type != TransactionType.ContractTransaction)
                        continue;
                    var states = await _repository.GetCoinStates(txHash);
                    for (var j = 0; j < states.Length; j++)
                    {
                        if (states[j].HasFlag(CoinState.Spent))
                            continue;
                        var output = tx.Outputs[j];
                        if (!output.ScriptHash.Equals(address))
                            continue;
                        var coinReference = new CoinReference
                        {
                            PrevHash = txHash,
                            PrevIndex = (ushort) j
                        };
                        outputs.Add(coinReference);
                    }
                }
            }
            /* TODO: "this code can be optimized a bit" */
            return outputs;
        }
    }
}