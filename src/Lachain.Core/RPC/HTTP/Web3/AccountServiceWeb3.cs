using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;
using Lachain.Crypto;
using Lachain.Utility.Serialization;


namespace Lachain.Core.RPC.HTTP.Web3
{
    public class AccountServiceWeb3 : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly ISystemContractReader _systemContractReader;
        private readonly ITransactionPool _transactionPool;

        


        public AccountServiceWeb3(IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer,
            IContractRegisterer contractRegisterer,
            ISystemContractReader systemContractReader, ITransactionPool transactionPool)
        {
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
            _contractRegisterer = contractRegisterer;
            _systemContractReader = systemContractReader;
            _transactionPool = transactionPool;
        }

        [JsonRpcMethod("eth_getBalance")]
        public string GetBalance(string address, string tag)
        {
            var addressUint160 = address.HexToUInt160();
            var availableBalance = GetSnapshotByTag(tag)!.Balances.GetBalance(addressUint160);
            return Web3DataFormatUtils.Web3Number(availableBalance.ToWei().ToUInt256());
        }

        [JsonRpcMethod("eth_getTransactionCount")]
        public ulong GetTransactionCount(string from, string blockId)
        {
            if(blockId.Equals("pending")) return _transactionPool.GetNextNonceForAddress(from.HexToUInt160());
            return GetSnapshotByTag(blockId)!.Transactions.GetTotalTransactionCount(from.HexToUInt160());
        }

        [JsonRpcMethod("eth_getCode")]
        public string GetCode(string contractAddr, string blockId)
        {

            var hash = contractAddr.HexToUInt160();
            if(hash is null) return "";

            if(!blockId.Equals("pending"))
            {
                var snapshot = GetSnapshotByTag(blockId);
                if(snapshot is null) return "";
                var contractByHash = snapshot.Contracts.GetContractByHash(hash);
                return contractByHash != null ? Web3DataFormatUtils.Web3Data(contractByHash!.ByteCode) : "";
            }

            // getting Code for "pending" 
            // look for the code in latest snapshot first
            var contractFromLatest = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(hash);
            if(contractFromLatest != null)
                return Web3DataFormatUtils.Web3Data(contractFromLatest!.ByteCode);
            
            // look for the code in pool
            var txHashPool = _transactionPool.Transactions.Keys;
            foreach(var txHash in txHashPool)
            {
                var receipt = _transactionPool.GetByHash(txHash);
                if(receipt is null) continue;
                if (receipt.Transaction.To.Buffer.IsEmpty || receipt.Transaction.To.IsZero()) // this is deploy transaction
                {
                    // find the contract address where this contract will be deployed
                    var address = UInt160Utils.Zero.ToBytes().Ripemd();
                    if (receipt.Transaction?.From != null)
                    {
                        address = receipt.Transaction.From.ToBytes()
                        .Concat(receipt.Transaction.Nonce.ToBytes())
                        .Ripemd();
                    }
                    if(address.Equals(hash) && receipt!.Transaction != null)
                        return Web3DataFormatUtils.Web3Data(receipt!.Transaction!.Invocation.ToArray());
                }
            }
            
            // contract was not found anywhere
            return "";
        }
        
        [JsonRpcMethod("eth_accounts")]
        public JArray GetAccounts()
        {
            return new JArray {Web3DataFormatUtils.Web3Data(_systemContractReader.NodeAddress())};
        }

        [JsonRpcMethod("eth_sign")]
        private string Sign(string address, string message)
        {
            // TODO: implement message signing
            //var addressUint160 = address.HexToUInt160();
            return Web3DataFormatUtils.Web3Data("".HexToBytes());
            //throw new ApplicationException("Not implemented yet");
        }

        [JsonRpcMethod("eth_getCompilers")]
        private JArray GetCompilers()
        {
            return new JArray();
        }

        [JsonRpcMethod("eth_compileLLL")]
        private string CompileLLL(string sourceCode)
        {
            return Web3DataFormatUtils.Web3Data("".HexToBytes());
            //throw new ApplicationException("Not implemented");
        }

        [JsonRpcMethod("eth_compileSolidity")]
        private string CompileSolidity(string sourceCode)
        {
            return Web3DataFormatUtils.Web3Data("".HexToBytes());
            //throw new ApplicationException("Not implemented");
        }

        [JsonRpcMethod("eth_compileSerpent")]
        private string CompileSerpent(string sourceCode)
        {
            return Web3DataFormatUtils.Web3Data("".HexToBytes());
            //throw new ApplicationException("Not implemented");
        }

        private IBlockchainSnapshot? GetSnapshotByTag(string tag)
        {
            switch (tag)
            {
                case "latest":
                    return _stateManager.LastApprovedSnapshot;
                case "earliest":
                    return _snapshotIndexer.GetSnapshotForBlock(0);
                case "pending":
                    return _stateManager.PendingSnapshot;
                default:
                {
                    var blockNum = tag.HexToUlong();
                    return _snapshotIndexer.GetSnapshotForBlock(blockNum);
                }
            }
        }
    }
}