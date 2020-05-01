using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.VM;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class AccountServiceWeb3 : JsonRpcService
    {
        private readonly IVirtualMachine _virtualMachine;
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;

        public AccountServiceWeb3(
            IVirtualMachine virtualMachine,
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionPool transactionPool)
        {
            _virtualMachine = virtualMachine;
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionPool = transactionPool;
        }

        [JsonRpcMethod("eth_getBalance")]
        private string GetBalance(string address, string tag)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var availableBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);
            return availableBalance.ToWei().ToUInt160().ToBytes().Reverse()
                // .SkipWhile(b => b == 0)
                .ToHex();
        }

        [JsonRpcMethod("eth_getTransactionCount")]
        private ulong GetTransactionCount(string from, string blockId)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return nonce;
        }
        private ulong GetTransactionCount(string from)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return nonce;
        }
        
        [JsonRpcMethod("eth_getCode")]
        private string GetCode(string contract, string blockId)
        {
            /*
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                contract.HexToUInt160());
            return contractByHash is null ? "0x" : "0x1";
            */
                
            // hardcoded to prevent default 21000 gas in Metamask
            return "0x1";
        }
    }
}