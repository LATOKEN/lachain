using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Core.Blockchain.Genesis;
using Google.Protobuf;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        private static readonly Func<byte[], byte[]> ToAddress = CryptoProvider.GetCrypto().ComputeAddress;
        private static readonly ILogger<GovernanceContract> Logger =
            LoggerFactory.GetLoggerForClass<GovernanceContract>();

        private readonly StorageVariable _consensusGeneration;
        private readonly StorageVariable _nextValidators;
        private readonly StorageMapping _confirmations;
        private readonly StorageVariable _blockReward;
        private readonly StorageVariable _playersCount;
        private readonly StorageVariable _tsKeys;
        private readonly StorageVariable _tpkeKey;
        private readonly StorageVariable _collectedFees;

        public GovernanceContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _consensusGeneration = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
            _nextValidators = new StorageVariable(
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
            _playersCount = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(4).ToUInt256()
            );
            _tsKeys = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(5).ToUInt256()
            );
            _tpkeKey = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(6).ToUInt256()
            );
            _collectedFees = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(7).ToUInt256()
            );
            TryInitStorage();
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        [ContractMethod(GovernanceInterface.MethodChangeValidators)]
        public void ChangeValidators(byte[][] newValidators)
        {
            if (!MsgSender().Equals(ContractRegisterer.StakingContract))
                throw new Exception("Auth failure");
            
            _nextValidators.Set(newValidators
                .Select(x => x.ToPublicKey().EncodeCompressed())
                .Flatten()
                .ToArray()
            );
        }

        [ContractMethod(GovernanceInterface.MethodDistributeCycleRewardsAndPenalties)]
        public void DistributeCycleRewardsAndPenalties()
        {
            if (!MsgSender().IsZero())
            {
                throw new Exception("Auth failure");
            }
            
            var txFeesAmount = GetCollectedFees();
            
            if (txFeesAmount > Money.Zero)
            {
                _contractContext.Snapshot.Balances.SubBalance(
                    ContractRegisterer.GovernanceContract, txFeesAmount
                );
            }
            
            var totalReward = GetBlockReward().ToMoney(true) * (int) StakingContract.CycleDuration + txFeesAmount;
            
            _contractContext.Sender = ContractRegisterer.GovernanceContract;
            var staking = new StakingContract(_contractContext);
            staking.DistributeRewardsAndPenalties(totalReward);
        }

        private void TryInitStorage()
        {
            if (_blockReward.Get().Length == 0)
            {
                _blockReward.Set(BigInteger.Parse(GenesisConfig.BlockReward).ToUInt256(true).ToBytes());
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
            var tsKeys = new PublicKeySet(thresholdSignaturePublicKeys.Select(x => PublicKey.FromBytes(x)), faulty);
            var tpkeKey = Crypto.TPKE.PublicKey.FromBytes(tpkePublicKey);
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);
            SetConfirmations(keyringHash.ToBytes(), gen, votes + 1);

            if (votes + 1 != players - faulty) return;

            SetPlayersCount(players);
            SetTSKeys(tsKeys);
            SetTPKEKey(tpkeKey);
        }

        [ContractMethod(GovernanceInterface.MethodFinishCycle)]
        public void FinishCycle()
        {
            var players = GetPlayersCount();
            var faulty = (players - 1) / 3;
            var tsKeys = GetTSKeys();
            var tpkeKey = GetTPKEKey();
            
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);

            if (votes + 1 < players - faulty) 
                throw new Exception("Impossible to finish cycle. Not enough votes.");

            var ecdsaPublicKeys = _nextValidators.Get()
                .Batch(Crypto.CryptoUtils.PublicKeyLength)
                .Select(x => x.ToArray().ToPublicKey())
                .ToArray();

            _contractContext.Snapshot.Validators.UpdateValidators(ecdsaPublicKeys, tsKeys, tpkeKey);
            _contractContext.Snapshot.Events.AddEvent(new Event
            {
               Contract = ContractRegisterer.GovernanceContract,
               Data = ByteString.Empty,
               Index = 0,
               TransactionHash = _contractContext.Receipt?.Hash
            });
            
            
            var laToken = new NativeTokenContract(_contractContext);
            var txFeesAmount = laToken.BalanceOf(ContractRegisterer.GovernanceContract).ToMoney(true);
            SetColelctedFees(txFeesAmount);
            
            SetConsensusGeneration(gen + 1); // this "clears" confirmations
            Logger.LogWarning("Enough confirmations collected, validators will be changed in the next block");
            Logger.LogWarning(
                $"  - ECDSA public keys: {string.Join(", ", ecdsaPublicKeys.Select(key => key.ToHex()))}");
            Logger.LogWarning($"  - TS public keys: {string.Join(", ", tsKeys.Keys.Select(key => key.ToHex()))}");
            Logger.LogWarning($"  - TPKE public key: {tpkeKey.ToHex()}");
        }

        private ulong GetConsensusGeneration()
        {
            var gen = _consensusGeneration.Get();
            return gen.Length == 0 ? 0 : gen.AsReadOnlySpan().ToUInt64();
        }
        
        private UInt256 GetBlockReward()
        {
            var reward = _blockReward.Get();
            return reward.ToUInt256();
        }

        private void SetConsensusGeneration(ulong generation)
        {
            _consensusGeneration.Set(generation.ToBytes().ToArray());
        }

        private void SetPlayersCount(int count)
        {
            _playersCount.Set(count.ToBytes().ToArray());
        }
        
        private int GetPlayersCount()
        {
            var count = _playersCount.Get();
            return count.AsReadOnlySpan().ToInt32();
        }

        private void SetTSKeys(PublicKeySet tsKeys)
        {
            _tsKeys.Set(tsKeys.ToBytes().ToArray());
        }
        
        private PublicKeySet GetTSKeys()
        {
            var tsKeys = _tsKeys.Get();
            return PublicKeySet.FromBytes(tsKeys);
        }

        private void SetColelctedFees(Money fees)
        {
            _collectedFees.Set(fees.ToUInt256(true).ToBytes());
        }
        
        private Money GetCollectedFees()
        {
            var fees = _collectedFees.Get();
            return fees.ToUInt256().ToMoney(true);
        }

        private void SetTPKEKey(Crypto.TPKE.PublicKey tpkeKey)
        {
            _tpkeKey.Set(tpkeKey.ToBytes().ToArray());
        }
        
        private Crypto.TPKE.PublicKey GetTPKEKey()
        {
            var tpkeKey = _tpkeKey.Get();
            return Crypto.TPKE.PublicKey.FromBytes(tpkeKey);
        }

        private int GetConfirmations(IEnumerable<byte> key, ulong gen)
        {
            var votes = _confirmations.GetValue(key);
            if (votes.Length == 0) return 0;
            if (votes.AsReadOnlySpan().ToUInt64() != gen) return 0;
            return votes.AsReadOnlySpan().Slice(8).ToInt32();
        }

        private void SetConfirmations(IEnumerable<byte> key, ulong gen, int votes)
        {
            _confirmations.SetValue(key, gen.ToBytes().Concat(votes.ToBytes()).ToArray());
        }

        public bool IsNextValidator(byte[] publicKey)
        {
            var validators = _nextValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    return true;
                }
            }

            return false;
        }

        public byte[][] GetNextValidators()
        {
            var validators = new byte[][]{};
            var data = _nextValidators.Get();
            for (var startByte = 0; startByte < data.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = data.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Append(validator).ToArray();
            }

            return validators;
        }

        private UInt160 MsgSender()
        {
            return _contractContext.Sender ?? throw new InvalidOperationException();
        }
    }
}