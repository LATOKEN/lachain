using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using NBitcoin;
using Phorkus.Core.Blockchain;
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

        public AccountService(ITransactionBuilder transactionBuilder, IStateManager stateManager)
        {
            _transactionBuilder = transactionBuilder;
            _stateManager = stateManager;
        }

        public override Task<SendAcceptedTransactionReply> SendAcceptedTransaction(SendAcceptedTransactionRequest request,
            ServerCallContext context)
        {
            return base.SendAcceptedTransaction(request, context);
        }
        
        public override Task<CreateContractTransactionReply> CreateContractTransaction(
            CreateContractTransactionRequest request, ServerCallContext context)
        {
            var tx = _transactionBuilder.TransferTransaction(request.From, request.To, request.Asset,
                request.Value.ToMoney());
            var reply = new CreateContractTransactionReply
            {
                Transaction = tx,
                Hash = tx.ToHash256()
            };
            return Task.FromResult(reply);
        }

        public override Task<CreateDeployTransactionReply> CreateDeployTransaction(
            CreateDeployTransactionRequest request, ServerCallContext context)
        {
            var tx = _transactionBuilder.DeployTransaction(request.From, request.Abi, request.Wasm, request.Version);
            var reply = new CreateDeployTransactionReply
            {
                Transaction = tx,
                Hash = tx.ToHash256()
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
    }
}