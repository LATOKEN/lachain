using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class AccountServiceWeb3 : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly ISystemContractReader _systemContractReader;


        public AccountServiceWeb3(IStateManager stateManager, IContractRegisterer contractRegisterer,
            ISystemContractReader systemContractReader)
        {
            _stateManager = stateManager;
            _contractRegisterer = contractRegisterer;
            _systemContractReader = systemContractReader;
        }

        [JsonRpcMethod("eth_getBalance")]
        private string GetBalance(string address, string tag)
        {
            var addressUint160 = address.HexToUInt160();
            var availableBalance = GetSnapshotByTag(tag)!.Balances.GetBalance(addressUint160);
            return Web3DataFormatUtils.Web3Number(availableBalance.ToWei().ToUInt256());
        }

        [JsonRpcMethod("eth_getTransactionCount")]
        private ulong GetTransactionCount(string from, string blockId)
        {
            return GetSnapshotByTag(blockId)!.Transactions.GetTotalTransactionCount(from.HexToUInt160());
        }

        [JsonRpcMethod("eth_getCode")]
        private string GetCode(string contractAddr, string blockId)
        {
            // var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
            //     contractAddr.HexToUInt160());
            // var systemContract = _contractRegisterer.GetContractByAddress(contractAddr.HexToUInt160());
            // return contractByHash is null ? "0x" : "0x1";
            
                
            // hardcoded to prevent default 21000 gas in Metamask
            return "0x01";
        }
        
        [JsonRpcMethod("eth_accounts")]
        private JArray GetAccounts()
        {
            return new JArray {Web3DataFormatUtils.Web3Data(_systemContractReader.NodeAddress())};
        }

        [JsonRpcMethod("eth_sign")]
        private string Sign(string address, string message)
        {
            // TODO: implement message signing
            //var addressUint160 = address.HexToUInt160();
            //return Web3DataFormatUtils.Web3Data("".HexToBytes());
            throw new ApplicationException("Not implemented yet");
        }

        private IBlockchainSnapshot? GetSnapshotByTag(string tag)
        {
            switch (tag)
            {
                case "latest":
                    return _stateManager.LastApprovedSnapshot;
                case "earliest":
                    // TODO: return address balance after genesis block
                    throw new ArgumentException("Earliest block is not supported now");
                case "pending":
                    return _stateManager.PendingSnapshot;
                default:
                {
                    var blockNum = tag.HexToUlong();
                    // TODO: return address balance for given block
                    throw new ArgumentException("Previous blocks are not supported now");
                }
            }
        }
    }
}