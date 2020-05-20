using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        private static readonly ILogger<GovernanceContract> Logger =
            LoggerFactory.GetLoggerForClass<GovernanceContract>();

        private readonly StorageVariable _consensusGeneration;
        private readonly StorageVariable _pendingValidators;
        private readonly StorageMapping _confirmations;

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
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        [ContractMethod(GovernanceInterface.MethodChangeValidators)]
        public ExecutionStatus ChangeValidators(byte[][] newValidators, SystemContractExecutionFrame frame)
        {
            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.ChangeValidatorsCost);
            _pendingValidators.Set(newValidators
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
            // TODO: validate everything
            frame.ReturnValue = new byte[] { };
            frame.UseGas(GasMetering.KeygenCommitCost);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(GovernanceInterface.MethodKeygenSendValue)]
        public ExecutionStatus KeyGenSendValue(UInt256 proposer, byte[][] encryptedValues,
            SystemContractExecutionFrame frame)
        {
            // TODO: validate everything
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
            var tsKeys = new PublicKeySet(thresholdSignaturePublicKeys.Select(x => PublicKey.FromBytes(x)), faulty);
            var tpkeKey = Crypto.TPKE.PublicKey.FromBytes(tpkePublicKey);
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();

            var gen = GetConsensusGeneration();
            var votes = GetConfirmations(keyringHash.ToBytes(), gen);
            SetConfirmations(keyringHash.ToBytes(), gen, votes + 1);

            if (votes + 1 != players - faulty) return ExecutionStatus.Ok;

            var ecdsaPublicKeys = _pendingValidators.Get()
                .Batch(CryptoUtils.PublicKeyLength)
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
            SetConsensusGeneration(gen + 1); // this "clears" confirmations
            Logger.LogWarning("Enough confirmations collected, validators will be changed in the next block");
            Logger.LogWarning(
                $"  - ECDSA public keys: {string.Join(", ", ecdsaPublicKeys.Select(key => key.ToHex()))}");
            Logger.LogWarning($"  - TS public keys: {string.Join(", ", tsKeys.Keys.Select(key => key.ToHex()))}");
            Logger.LogWarning($"  - TPKE public key: {tpkeKey.ToHex()}");
            return ExecutionStatus.Ok;
        }

        private ulong GetConsensusGeneration()
        {
            var gen = _consensusGeneration.Get();
            return gen.Length == 0 ? 0 : gen.AsReadOnlySpan().ToUInt64();
        }

        private void SetConsensusGeneration(ulong generation)
        {
            _consensusGeneration.Set(generation.ToBytes().ToArray());
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
    }
}