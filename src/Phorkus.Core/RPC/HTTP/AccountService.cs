using System.Linq;
using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.RPC.HTTP
{
    public class AccountService : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public AccountService(
            IStateManager stateManager,
            ITransactionPool transactionPool)
        {
            _stateManager = stateManager;
            _transactionPool = transactionPool;
        }
        
        [JsonRpcMethod("getAvailableAssets")]
        private JObject GetAvailableAssets()
        {
            var assetRepository = _stateManager.LastApprovedSnapshot.Assets;
            var assetHashesByName = _stateManager.LastApprovedSnapshot.Assets.GetAssetHashes()
                .Select(assetHash => assetRepository.GetAssetByHash(assetHash));
            var json = new JObject();
            foreach (var asset in assetHashesByName)
            {
                if (asset is null)
                    continue;
                json[asset.Name] = asset.Hash.Buffer.ToHex();
            }
            return json;
        }
        
        [JsonRpcMethod("getBalance")]
        private JObject GetBalance(string address, string assetHash)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var assetUint160 = assetHash.HexToBytes().ToUInt160();
            var availableBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetAvailableBalance(addressUint160, assetUint160);
            var withdrawingBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetWithdrawingBalance(addressUint160, assetUint160);
            var json = new JObject
            {
                ["available"] = availableBalance.ToUInt256().Buffer.ToHex(),
                ["withdrawing"] = withdrawingBalance.ToUInt256().Buffer.ToHex()
            };
            return json;
        }
        
        [JsonRpcMethod("sendRawTransaction")]
        private JObject SendRawTransaction(string rawTransation, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransation.HexToBytes());
            var json = new JObject
            {
                ["hash"] = transaction.ToHash256().Buffer.ToHex()
            };
            var result = _transactionPool.Add(
                transaction, signature.HexToBytes().ToSignature());
            if (result != OperatingError.Ok)
                json["failed"] = true;
            json["result"] = result.ToString();
            return json;
        }
        
        [JsonRpcMethod("getTotalTransactionCount")]
        private ulong GetTotalTransactionCount(string from)
        {
            var result = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return result;
        }
    }
}