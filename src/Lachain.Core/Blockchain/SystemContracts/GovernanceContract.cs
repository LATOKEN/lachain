using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        private static readonly ICrypto CryptoUtils = CryptoProvider.GetCrypto();
        private static readonly ILogger<GovernanceContract> Logger =
            LoggerFactory.GetLoggerForClass<GovernanceContract>();

        private readonly StorageVariable _consensusGeneration;
        private readonly StorageVariable _pendingValidators;
        private readonly StorageMapping _confirmations;
        private readonly StorageVariable _blockReward;

        public GovernanceContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _consensusGeneration = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
            _pendingValidators = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                BigInteger.One.ToUInt256()
            );
            _confirmations = new StorageMapping(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(2).ToUInt256()
            );
            _blockReward = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(3).ToUInt256()
            );
            TryInitStorage();
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        [ContractMethod(GovernanceInterface.MethodChangeValidators)]
        public void ChangeValidators(byte[][] newValidators)
        {
            /* TODO: only staking contract can call this function */ 
            _pendingValidators.Set(newValidators
                .Select(x => x.ToPublicKey().EncodeCompressed())
                .Flatten()
                .ToArray()
            );
        }

        [ContractMethod(GovernanceInterface.MethodDistributeBlockReward)]
        public void DistributeBlockReward()
        {
            var validators = 
                _contractContext.Snapshot.Validators.GetValidatorsPublicKeys().Select(x => PublicKeyToAddress(x.Buffer.ToByteArray())).ToArray();
            _contractContext.Sender = ContractRegisterer.GovernanceContract;
            validators = validators.Concat(new []{ContractRegisterer.PassiveStakingContract}).ToArray();
            var laToken = new NativeTokenContract(_contractContext);
            laToken.MintBlockRewards(validators, GetBlockReward().ToMoney());
        }

        private void TryInitStorage()
        {
            if (_blockReward.Get().Length == 0)
            {
                _blockReward.Set(BigInteger.Parse(GenesisConfig.BlockReward).ToUInt256().ToBytes());
            }
        }

        [ContractMethod(GovernanceInterface.MethodKeygenCommit)]
        public void KeyGenCommit(byte[] commitment, byte[][] encryptedRows)
        {
            // TODO: validate everything
        }

        [ContractMethod(GovernanceInterface.MethodKeygenSendValue)]
        public void KeyGenSendValue(UInt256 proposer, byte[][] encryptedValues)
        {
            // TODO: validate everything
        }

        [ContractMethod(GovernanceInterface.MethodKeygenConfirm)]
        public void KeyGenConfirm(byte[] tpkePublicKey, byte[][] thresholdSignaturePublicKeys)
        {
            var players = thresholdSignaturePublicKeys.Length;
            var faulty = (players - 1) / 3;
            var tsKeys = new PublicKeySet(thresholdSignaturePublicKeys.Select(PublicKey.FromBytes), faulty);
            var tpkeKey = Crypto.TPKE.PublicKey.FromBytes(tpkePublicKey);
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);
            SetConfirmations(keyringHash.ToBytes(), gen, votes + 1);
            
            if (votes + 1 != players - faulty) return;
            
            var ecdsaPublicKeys = _pendingValidators.Get()
                .Batch(Crypto.CryptoUtils.PublicKeyLength)
                .Select(x => x.ToArray().ToPublicKey())
                .ToArray();

            _contractContext.Snapshot.Validators.UpdateValidators(ecdsaPublicKeys, tsKeys, tpkeKey);
            SetConsensusGeneration(gen + 1); // this "clears" confirmations
            Logger.LogWarning("Enough confirmations collected, validators will be changed in the next block");
            Logger.LogWarning($"  - ECDSA public keys: {string.Join(", ", ecdsaPublicKeys.Select(key => key.ToHex()))}");
            Logger.LogWarning($"  - TS public keys: {string.Join(", ", tsKeys.Keys.Select(key => key.ToBytes().ToHex()))}");
            Logger.LogWarning($"  - TPKE public key: {tpkeKey.ToBytes().ToHex()}");
        }
        
        private int GetConsensusGeneration()
        {
            var gen = _consensusGeneration.Get();
            return gen.Length == 0 ? 0 : BitConverter.ToInt32(gen);
        }
        
        private UInt256 GetBlockReward()
        {
            var reward = _blockReward.Get();
            return reward.ToUInt256();
        }

        private void SetConsensusGeneration(int generation)
        {
            _consensusGeneration.Set(BitConverter.GetBytes(generation));
        }
        
        private int GetConfirmations(IEnumerable<byte> key, int gen)
        {
            var votes = _confirmations.GetValue(key);
            if (votes.Length == 0) return 0;
            if (BitConverter.ToInt32(votes, 0) != gen) return 0;
            return BitConverter.ToInt32(votes, 4);
        }

        private void SetConfirmations(IEnumerable<byte> key, int gen, int votes)
        {
            _confirmations.SetValue(key, BitConverter.GetBytes(gen).Concat(BitConverter.GetBytes(votes)).ToArray());
        }

        private UInt160 PublicKeyToAddress(byte[] publicKey)
        {
            return CryptoUtils.ComputeAddress(publicKey).ToUInt160();
        }
    }
}