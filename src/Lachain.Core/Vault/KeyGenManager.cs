using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Vault
{
    public class KeyGenManager
    {
        private static readonly ILogger<KeyGenManager> Logger = LoggerFactory.GetLoggerForClass<KeyGenManager>();

        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;
        private TrustlessKeygen? _currentKeygen;
        private IDictionary<UInt256, int> _confirmations;

        public KeyGenManager(
            ITransactionManager transactionManager,
            ITransactionBuilder transactionBuilder,
            IPrivateWallet privateWallet,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner
        )
        {
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _confirmations = new Dictionary<UInt256, int>();
            transactionManager.OnSystemContractInvoked += TransactionManagerOnOnSystemContractInvoked;
        }

        private void TransactionManagerOnOnSystemContractInvoked(object _, ContractContext context)
        {
            var tx = context.Receipt.Transaction;
            if (!tx.To.Equals(ContractRegisterer.GovernanceContract)) return;
            if (tx.Invocation.Length < 4) return;

            var signature = BitConverter.ToUInt32(tx.Invocation.Take(4).ToArray(), 0);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodChangeValidators))
            {
                var args = decoder.Decode(GovernanceInterface.MethodChangeValidators);
                var publicKeys =
                    (args[0] as byte[][] ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                if (!publicKeys.Contains(_privateWallet.EcdsaKeyPair.PublicKey)) return;
                if (_currentKeygen != null)
                    throw new ArgumentException("Cannot start keygen, since one is already running");
                var faulty = (publicKeys.Length - 1) / 3;
                _currentKeygen = new TrustlessKeygen(_privateWallet.EcdsaKeyPair, publicKeys, faulty);
                _confirmations = new Dictionary<UInt256, int>();
                _transactionPool.Add(MakeCommitTransaction(_currentKeygen.StartKeygen()));
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenCommit))
            {
                if (_currentKeygen is null) return;
                var sender = _currentKeygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                var commitment = Commitment.FromBytes(args[0] as byte[]);
                var encryptedRows = args[1] as byte[][];
                _transactionPool.Add(MakeSendValueTransaction(
                    _currentKeygen.HandleCommit(
                        sender,
                        new CommitMessage {Commitment = commitment, EncryptedRows = encryptedRows}
                    )
                ));
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenSendValue))
            {
                if (_currentKeygen is null) return;
                var sender = _currentKeygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
                var proposer = args[0] as UInt256;
                var encryptedValues = args[1] as byte[][];
                _currentKeygen.HandleSendValue(
                    sender,
                    new ValueMessage {Proposer = (int) proposer.ToBigInteger(), EncryptedValues = encryptedValues}
                );
                var keys = _currentKeygen.TryGetKeys();
                if (!keys.HasValue) return;
                Logger.LogDebug("NEW KEYS GENERATED!!!!!");
                _transactionPool.Add(MakeConfirmTransaction(keys.Value));
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenConfirm))
            {
                if (_currentKeygen is null) return;
                var sender = _currentKeygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                var keyringHash = args[0] as UInt256 ?? throw new Exception();

                _confirmations.PutIfAbsent(keyringHash, 0);
                _confirmations[keyringHash] += 1;

                if (_confirmations[keyringHash] != _currentKeygen.Players - _currentKeygen.Faulty) return;
                var keys = _currentKeygen.TryGetKeys() ?? throw new Exception();
                _privateWallet.AddThresholdSignatureKeyAfterBlock(
                    context.Receipt.Block, keys.ThresholdSignaturePrivateKey);
                _privateWallet.AddTpkePrivateKeyAfterBlock(context.Receipt.Block, keys.TpkePrivateKey);

                _currentKeygen = null;
                _confirmations.Clear();
            }
        }

        private TransactionReceipt MakeConfirmTransaction(ThresholdKeyring keyring)
        {
            var hash = keyring.TpkePublicKey.ToByteArray()
                .Concat(keyring.ThresholdSignaturePublicKeySet.Keys
                    .Select(x => x.ToByteArray())
                    .Cast<IEnumerable<byte>>()
                    .Aggregate((a, b) => a.Concat(b)))
                .Keccak();

            var tx = _transactionBuilder.InvokeTransaction(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenConfirm,
                hash
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TransactionReceipt MakeSendValueTransaction(ValueMessage valueMessage)
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenSendValue,
                new BigInteger(valueMessage.Proposer).ToUInt256(),
                valueMessage.EncryptedValues
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TransactionReceipt MakeCommitTransaction(CommitMessage commitMessage)
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenCommit,
                commitMessage.Commitment.ToBytes(),
                commitMessage.EncryptedRows
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }
    }
}