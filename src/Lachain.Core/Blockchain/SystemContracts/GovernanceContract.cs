using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly InvocationContext _context;

        private static readonly Func<byte[], byte[]> ToAddress = CryptoProvider.GetCrypto().ComputeAddress;

        private static readonly ILogger<GovernanceContract> Logger =
            LoggerFactory.GetLoggerForClass<GovernanceContract>();

        private static readonly ICrypto CryptoInstance = CryptoProvider.GetCrypto();

        private readonly StorageVariable _consensusGeneration;
        private readonly StorageVariable _nextValidators;
        private readonly StorageMapping _confirmations;
        private readonly StorageVariable _blockReward;
        private readonly StorageVariable _playersCount;
        private readonly StorageVariable _tsKeys;
        private readonly StorageVariable _tpkeKey;
        private readonly StorageVariable _collectedFees;

        public GovernanceContract(InvocationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _consensusGeneration = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
            _nextValidators = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                BigInteger.One.ToUInt256()
            );
            _confirmations = new StorageMapping(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(2).ToUInt256()
            );
            _blockReward = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(3).ToUInt256()
            );
            _playersCount = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(4).ToUInt256()
            );
            _tsKeys = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(5).ToUInt256()
            );
            _tpkeKey = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(6).ToUInt256()
            );
            _collectedFees = new StorageVariable(
                ContractRegisterer.GovernanceContract,
                context.Snapshot.Storage,
                new BigInteger(7).ToUInt256()
            );
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        [ContractMethod(GovernanceInterface.MethodDistributeCycleRewardsAndPenalties)]
        public ExecutionStatus DistributeCycleRewardsAndPenalties(SystemContractExecutionFrame frame)
        {
            if (!MsgSender().IsZero())
                return ExecutionStatus.ExecutionHalted;

            var txFeesAmount = GetCollectedFees();

            if (txFeesAmount > Money.Zero)
            {
                _context.Snapshot.Balances.SubBalance(
                    ContractRegisterer.GovernanceContract, txFeesAmount
                );
            }

            var totalReward = GetBlockReward().ToMoney() * (int) StakingContract.CycleDuration + txFeesAmount;
            _context.Sender = ContractRegisterer.GovernanceContract;
            var staking = new StakingContract(_context);
            staking.DistributeRewardsAndPenalties(totalReward.ToUInt256(), frame);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodChangeValidators)]
        public ExecutionStatus ChangeValidators(byte[][] newValidators, SystemContractExecutionFrame frame)
        {
            if (!MsgSender().Equals(ContractRegisterer.StakingContract) && !MsgSender().IsZero())
                return ExecutionStatus.ExecutionHalted;

            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.ChangeValidatorsCost);
            foreach (var publicKey in newValidators)
            {
                if (publicKey.Length != CryptoUtils.PublicKeyLength) return ExecutionStatus.ExecutionHalted;
                if (!CryptoInstance.TryDecodePublicKey(publicKey, false, out _))
                    return ExecutionStatus.ExecutionHalted;
            }

            _nextValidators.Set(newValidators
                .Select(x => x.ToPublicKey().EncodeCompressed())
                .Flatten()
                .ToArray()
            );
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodKeygenCommit)]
        public ExecutionStatus KeyGenCommit(byte[] commitment, byte[][] encryptedRows,
            SystemContractExecutionFrame frame)
        {
            try
            {
                var c = Commitment.FromBytes(commitment);
                if (!c.IsValid()) throw new Exception();
                var n = _nextValidators.Get().Length / CryptoUtils.PublicKeyLength;
                if (c.Degree != (n - 1) / 3) throw new Exception();
                if (encryptedRows.Length != n) throw new Exception();
            }
            catch
            {
                return ExecutionStatus.ExecutionHalted;
            }

            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.KeygenCommitCost);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodKeygenSendValue)]
        public ExecutionStatus KeyGenSendValue(UInt256 proposer, byte[][] encryptedValues,
            SystemContractExecutionFrame frame)
        {
            try
            {
                var n = _nextValidators.Get().Length / CryptoUtils.PublicKeyLength;
                var p = proposer.ToBigInteger();
                if (p < 0 || p >= n) throw new Exception();
                if (encryptedValues.Length != n) throw new Exception();
            }
            catch
            {
                return ExecutionStatus.ExecutionHalted;
            }

            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.KeygenSendValueCost);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodKeygenConfirm)]
        public ExecutionStatus KeyGenConfirm(byte[] tpkePublicKey, byte[][] thresholdSignaturePublicKeys,
            SystemContractExecutionFrame frame)
        {
            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.KeygenConfirmCost);
            var players = thresholdSignaturePublicKeys.Length;
            var faulty = (players - 1) / 3;
            UInt256 keyringHash;
            PublicKeySet tsKeys;
            try
            {
                tsKeys = new PublicKeySet(
                    thresholdSignaturePublicKeys.Select(x => Crypto.ThresholdSignature.PublicKey.FromBytes(x)),
                    faulty
                );
                var tpkeKey = PublicKey.FromBytes(tpkePublicKey);
                keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();
            }
            catch
            {
                return ExecutionStatus.ExecutionHalted;
            }

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);
            SetConfirmations(keyringHash.ToBytes(), gen, votes + 1);

            if (votes + 1 != players - faulty) return ExecutionStatus.Ok;
            Logger.LogDebug($"players count: {players}");
            SetPlayersCount(players);
            SetTSKeys(tsKeys);
            SetTpkeKey(tpkePublicKey);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodFinishCycle)]
        public ExecutionStatus FinishCycle(SystemContractExecutionFrame frame)
        {
            var players = GetPlayersCount();
            var faulty = (players - 1) / 3;
            var tsKeys = GetTSKeys();
            var tpkeKey = GetTpkeKey();
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);


            if (votes + 1 < players - faulty)
                return ExecutionStatus.ExecutionHalted;

            var ecdsaPublicKeys = _nextValidators.Get()
                .Batch(CryptoUtils.PublicKeyLength)
                .Select(x => x.ToArray().ToPublicKey())
                .ToArray();

            _context.Snapshot.Validators.UpdateValidators(ecdsaPublicKeys, tsKeys, tpkeKey);
            _context.Snapshot.Events.AddEvent(new Event
            {
                Contract = ContractRegisterer.GovernanceContract,
                Data = ByteString.Empty,
                Index = 0,
                TransactionHash = _context.Receipt?.Hash
            });

            var balanceOfExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, ContractRegisterer.GovernanceContract,
                Lrc20Interface.MethodBalanceOf,
                ContractRegisterer.GovernanceContract);

            if (balanceOfExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var txFeesAmount = balanceOfExecutionResult.ReturnValue!.ToUInt256().ToMoney();
            SetColelctedFees(txFeesAmount);
            SetConsensusGeneration(gen + 1); // this "clears" confirmations
            Logger.LogDebug("Enough confirmations collected, validators will be changed in the next block");
            Logger.LogDebug(
                $"  - ECDSA public keys: {string.Join(", ", ecdsaPublicKeys.Select(key => key.ToHex()))}");
            Logger.LogDebug($"  - TS public keys: {string.Join(", ", tsKeys.Keys.Select(key => key.ToHex()))}");
            Logger.LogDebug($"  - TPKE public key: {tpkeKey.ToHex()}");
            return ExecutionStatus.Ok;
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
            Logger.LogDebug(count.ToHex());
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
            _collectedFees.Set(fees.ToUInt256().ToBytes());
        }

        private Money GetCollectedFees()
        {
            var fees = _collectedFees.Get();
            return fees.ToUInt256().ToMoney();
        }

        private void SetTpkeKey(byte[] tpkePublicKey)
        {
            _tpkeKey.Set(tpkePublicKey);
        }

        private PublicKey GetTpkeKey()
        {
            var tpkePublicKey = _tpkeKey.Get();
            return PublicKey.FromBytes(tpkePublicKey);
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

        [ContractMethod(GovernanceInterface.MethodIsNextValidator)]
        public ExecutionStatus IsNextValidator(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.GovernanceIsNextValidatorCost);
            var result = false;
            var validators = _nextValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    result = true;
                }
            }

            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();

            return ExecutionStatus.Ok;
        }

        public byte[][] GetNextValidators()
        {
            return _nextValidators.Get().Batch(CryptoUtils.PublicKeyLength)
                .Select(x => x.ToArray())
                .ToArray();
        }

        private UInt160 MsgSender()
        {
            return _context.Sender ?? throw new InvalidOperationException();
        }
    }
}