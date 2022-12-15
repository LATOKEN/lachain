using System.Numerics;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking.PeerFault;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Network
{
    public class BannedPeerTracker : IBannedPeerTracker
    {
        // Scans for banned peer in system contract tx
        // If a peer is banned locally, then sends a system contract tx to broadcast
        private static readonly ILogger<BannedPeerTracker> Logger = LoggerFactory.GetLoggerForClass<BannedPeerTracker>();
        private readonly IPeerBanManager _banManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IPrivateWallet _privateWallet;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionPool _transactionPool;
        public BannedPeerTracker(
            IPeerBanManager banManager,
            ITransactionBuilder transactionBuilder,
            IPrivateWallet privateWallet,
            ITransactionSigner transactionSigner,
            IBlockManager blockManager,
            ITransactionPool transactionPool
        )
        {
            _banManager = banManager;
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _transactionSigner = transactionSigner;
            _blockManager = blockManager;
            _transactionPool = transactionPool;
            _banManager.OnPeerBanned += OnPeerBanned;
        }

        private void OnPeerBanned(object? sender, (byte[] publicKey, ulong penalties) @event)
        {
            Task.Factory.StartNew(() =>
            {
                var (publicKey, penalties) = @event;
                var receipt = MakeBanRequestTransaction(penalties, publicKey);
                var added = AddTransactionToPool(receipt);
                if (!added)
                {
                    Logger.LogWarning($"Falied to send MakeBanRequestTransaction tx");
                }
            }, TaskCreationOptions.LongRunning);
        }

        private bool AddTransactionToPool(TransactionReceipt receipt)
        {
            var error = _transactionPool.Add(receipt);
            if (error != OperatingError.Ok)
            {
                Logger.LogTrace($"Failed to add transaction {receipt.Hash} to pool with error {error}");
                return false;
            }
            return true;
        }

        private TransactionReceipt MakeBanRequestTransaction(ulong penalties, byte[] publicKey)
        {
            Logger.LogTrace("MakeBanRequestTransaction");
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodBanPeerRequestFrom,
                0,
                new BigInteger(penalties).ToUInt256(),
                publicKey,
                _privateWallet.EcdsaKeyPair.PublicKey.EncodeCompressed()
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
        }
    }
}