using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Core.Vault
{
    public class KeyGenManager : IKeyGenManager
    {
        private static readonly ILogger<KeyGenManager> Logger = LoggerFactory.GetLoggerForClass<KeyGenManager>();

        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;
        private TrustlessKeygen? _currentKeygen;
        private bool _confirmSent;
        private IDictionary<UInt256, int> _confirmations;

        public KeyGenManager(
            IBlockManager blockManager,
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
            blockManager.OnSystemContractInvoked += BlockManagerOnSystemContractInvoked;
        }

        private void BlockManagerOnSystemContractInvoked(object _, ContractContext context)
        {
            if (context.Receipt is null) return;
            var tx = context.Receipt.Transaction;
            if (!tx.To.Equals(ContractRegisterer.GovernanceContract)) return;
            if (tx.Invocation.Length < 4) return;

            var signature = ContractEncoder.MethodSignatureAsInt(tx.Invocation);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodChangeValidators))
            {
                Logger.LogInformation(
                    $"Detected call of GovernanceContract.{GovernanceInterface.MethodChangeValidators}");
                var args = decoder.Decode(GovernanceInterface.MethodChangeValidators);
                var publicKeys =
                    (args[0] as byte[][] ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                if (!publicKeys.Contains(_privateWallet.EcdsaKeyPair.PublicKey))
                {
                    Logger.LogInformation("Skipping validator change event since we are not new validator");
                    return;
                }

                if (_currentKeygen != null)
                    throw new ArgumentException("Cannot start keygen, since one is already running");
                var faulty = (publicKeys.Length - 1) / 3;
                _currentKeygen = new TrustlessKeygen(_privateWallet.EcdsaKeyPair, publicKeys, faulty);
                _confirmSent = false;
                _confirmations = new Dictionary<UInt256, int>();
                var commitTx = MakeCommitTransaction(_currentKeygen.StartKeygen());
                if (_transactionPool.Add(commitTx) is var error && error != OperatingError.Ok)
                    Logger.LogError($"Error creating commit transaction ({commitTx.Hash.ToHex()}): {error}");
                else
                {
                    Logger.LogInformation(
                        $"Commit transaction {commitTx.Hash.ToHex()} successfully sent: " +
                        $"tx={commitTx.Hash.ToHex()} from={commitTx.Transaction.From.ToHex()} nonce={commitTx.Transaction.Nonce}"
                    );
                }
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenCommit))
            {
                Logger.LogInformation($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenCommit}");
                if (_currentKeygen is null) return;
                var sender = _currentKeygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                var commitment = Commitment.FromBytes(args[0] as byte[]);
                var encryptedRows = args[1] as byte[][];
                var sendValueTx = MakeSendValueTransaction(
                    _currentKeygen.HandleCommit(
                        sender,
                        new CommitMessage {Commitment = commitment, EncryptedRows = encryptedRows}
                    )
                );
                if (_transactionPool.Add(sendValueTx) is var error && error != OperatingError.Ok)
                    Logger.LogError($"Error creating send value transaction ({sendValueTx.Hash.ToHex()}): {error}");
                else
                {
                    Logger.LogInformation(
                        $"Send value transaction {sendValueTx.Hash.ToHex()} successfully sent: " +
                        $"tx={sendValueTx.Hash.ToHex()} from={sendValueTx.Transaction.From.ToHex()} nonce={sendValueTx.Transaction.Nonce}"
                    );
                }
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenSendValue))
            {
                Logger.LogInformation(
                    $"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenSendValue}");
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
                if (!keys.HasValue || _confirmSent) return;
                var confirmTx = MakeConfirmTransaction(keys.Value);
                if (_transactionPool.Add(confirmTx) is var error && error != OperatingError.Ok)
                    Logger.LogError($"Error creating confirm transaction ({confirmTx.Hash.ToHex()}): {error}");
                else
                {
                    _confirmSent = true;
                    Logger.LogInformation(
                        $"Confirm transaction {confirmTx.Hash.ToHex()} for hash {keys.Value.PublicPartHash().ToHex()} successfully sent: " +
                        $"tx={confirmTx.Hash.ToHex()} from={confirmTx.Transaction.From.ToHex()} nonce={confirmTx.Transaction.Nonce}"
                    );
                }
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenConfirm))
            {
                Logger.LogInformation($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenConfirm}");
                if (_currentKeygen is null) return;
                var sender = _currentKeygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                var tpkePublicKey = PublicKey.FromBytes(args[0] as byte[] ?? throw new Exception());
                var tsKeys = new PublicKeySet(
                    (args[1] as byte[][] ?? throw new Exception()).Select(Crypto.ThresholdSignature.PublicKey
                        .FromBytes),
                    _currentKeygen.Faulty
                );
                var keyringHash = tpkePublicKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();
                // TODO: somehow calculate confirmations in contract and trigger this by event

                _confirmations.PutIfAbsent(keyringHash, 0);
                _confirmations[keyringHash] += 1;

                Logger.LogInformation(
                    $"Collected {_confirmations[keyringHash]} confirmations for hash {keyringHash.ToHex()}");
                if (_confirmations[keyringHash] != _currentKeygen.Players - _currentKeygen.Faulty) return;
                var keys = _currentKeygen.TryGetKeys() ?? throw new Exception();
                Logger.LogWarning($"Generated keyring with public hash {keys.PublicPartHash().ToHex()}");
                if (!keyringHash.Equals(keys.PublicPartHash()))
                {
                    throw new Exception();
                }

                // Logger.LogWarning($"  - TPKE private key: {keys.TpkePrivateKey.ToBytes().ToHex()}");
                Logger.LogWarning($"  - TPKE public key: {keys.TpkePublicKey.ToBytes().ToHex()}");
                // Logger.LogWarning($"  - TS private key: {keys.ThresholdSignaturePrivateKey.ToBytes().ToHex()}");
                Logger.LogWarning(
                    "  - TS public key: " +
                    keys.ThresholdSignaturePrivateKey.GetPublicKeyShare().ToBytes().ToHex()
                );
                Logger.LogWarning(
                    "  - TS public key set: " +
                    string.Join(", ", keys.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes().ToHex()))
                );
                _privateWallet.AddThresholdSignatureKeyAfterBlock(
                    context.Receipt.Block, keys.ThresholdSignaturePrivateKey);
                _privateWallet.AddTpkePrivateKeyAfterBlock(context.Receipt.Block, keys.TpkePrivateKey);
                Logger.LogInformation("Keyring saved to wallet");
                _currentKeygen = null;
                _confirmations.Clear();
            }
        }

        private TransactionReceipt MakeConfirmTransaction(ThresholdKeyring keyring)
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenConfirm,
                keyring.TpkePublicKey.ToBytes(),
                keyring.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray()
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