using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.Interface
{
    public class BlockchainManager : IBlockchainManager, IBlockchainContext
    {
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockManager _blockManager;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;

        public ulong CurrentBlockHeight => _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
        public Block? CurrentBlock => _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(CurrentBlockHeight);

        public BlockchainManager(
            IGenesisBuilder genesisBuilder,
            IBlockManager blockManager,
            IConfigManager configManager,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexRepository
        )
        {
            _genesisBuilder = genesisBuilder;
            _blockManager = blockManager;
            _configManager = configManager;
            _stateManager = stateManager;
            _snapshotIndexRepository = snapshotIndexRepository;
        }

        public bool TryBuildGenesisBlock()
        {
            var genesisBlock = _genesisBuilder.Build();
            if (_stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0) != null)
                return false;
            var snapshot = _stateManager.NewSnapshot();
            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis");
            genesisConfig.ValidateOrThrow();
            var initialConsensusState = new ConsensusState
            {
                TpkePublicKey = ByteString.CopyFrom(genesisConfig.ThresholdEncryptionPublicKey.HexToBytes()),
                TpkeVerificationKey =
                    ByteString.CopyFrom(genesisConfig.ThresholdEncryptionVerificationKey.HexToBytes()),
            };
            initialConsensusState.Validators.Add(genesisConfig.Validators.Select(v => new ValidatorCredentials
            {
                PublicKey = v.EcdsaPublicKey.HexToBytes().ToPublicKey(),
                ResolvableAddress = v.ResolvableName,
                ThresholdSignaturePublicKey = ByteString.CopyFrom(v.ThresholdSignaturePublicKey.HexToBytes()),
            }));
            snapshot.Validators.SetConsensusState(initialConsensusState);
            _stateManager.Approve();
            var error = _blockManager.Execute(genesisBlock.Block, genesisBlock.Transactions, commit: false,
                checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
            _stateManager.Commit();
            _blockManager.BlockPersisted(genesisBlock.Block);
            return true;
        }

        public UInt256 CalcStateHash(Block block, IEnumerable<TransactionReceipt> transactionReceipts)
        {
            var (operatingError, removeTransactions, stateHash, relayTransactions) =
                _blockManager.Emulate(block, transactionReceipts);
            if (operatingError != OperatingError.Ok)
                throw new InvalidBlockException(operatingError);
            if (removeTransactions.Count > 0)
                throw new InvalidBlockException(OperatingError.InvalidTransaction);
            if (relayTransactions.Count > 0)
                throw new InvalidBlockException(OperatingError.BlockGasOverflow);
            return stateHash;
        }

        public void PersistBlockManually(Block block, IEnumerable<TransactionReceipt> transactions)
        {
            var error = _blockManager.Execute(block, transactions, commit: true, checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
        }
    }
}