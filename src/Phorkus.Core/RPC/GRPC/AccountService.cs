using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Proto.Grpc;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.RPC.GRPC
{
    public class AccountService : Proto.Grpc.AccountService.AccountServiceBase
    {
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public AccountService(
            ITransactionBuilder transactionBuilder,
            IStateManager stateManager,
            ITransactionPool transactionPool)
        {
            _transactionBuilder = transactionBuilder;
            _stateManager = stateManager;
            _transactionPool = transactionPool;
        }

        public override Task<SendAcceptedTransactionReply> SendAcceptedTransaction(SendAcceptedTransactionRequest request,
            ServerCallContext context)
        {
            if (_transactionPool.Add(request.Transaction, request.Signature) != OperatingError.Ok)
                throw new Exception("Unable to add transaction to pool");
            var txHash = request.Transaction.ToHash256();
            var reply = new SendAcceptedTransactionReply
            {
                Hash = txHash
            };
            return Task.FromResult(reply);
        }

        public override Task<GetBalanceReply> GetBalance(GetBalanceRequest request, ServerCallContext context)
        {
            var balance = _stateManager.LastApprovedSnapshot.Balances.GetAvailableBalance(request.Address, request.AssetHash);
            var reply = new GetBalanceReply
            {
                AssetHash = request.AssetHash,
                Balance = balance.ToUInt256()
            };
            return Task.FromResult(reply);
        }

        public override Task<GetAvailableAssetsReply> GetAvailableAssets(GetAvailableAssetsRequest request,
            ServerCallContext context)
        {
            var assetRepository = _stateManager.LastApprovedSnapshot.Assets;
            var assetHashesByName = _stateManager.LastApprovedSnapshot.Assets.GetAssetHashes()
                .Select(assetHash => assetRepository.GetAssetByHash(assetHash))
                .Where(asset => asset != null)
                .ToDictionary(asset => asset.Name, asset => asset.Hash);
            var reply = new GetAvailableAssetsReply
            {
                Assets = { assetHashesByName }
            };
            return Task.FromResult(reply);
        }

        public override Task<CalcTransactionNonceReply> CalcTransactionNonce(CalcTransactionNonceRequest request, ServerCallContext context)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(request.From);
            var reply = new CalcTransactionNonceReply
            {
                Nonce = nonce
            };
            return Task.FromResult(reply);
        }
    }
}