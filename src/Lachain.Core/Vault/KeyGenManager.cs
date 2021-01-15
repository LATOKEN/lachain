using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.ComTypes;
using Lachain.Consensus;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Network;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Core.Vault
{
    public class KeyGenManager : IKeyGenManager
    {
        private static readonly ILogger<KeyGenManager> Logger = LoggerFactory.GetLoggerForClass<KeyGenManager>();

        private readonly IBlockManager _blockManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IKeyGenRepository _keyGenRepository;
        private readonly IBlockSynchronizer _blockSynchronizer;

        public KeyGenManager(
            IBlockManager blockManager,
            ITransactionManager transactionManager,
            ITransactionBuilder transactionBuilder,
            IPrivateWallet privateWallet,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            IKeyGenRepository keyGenRepository,
            IBlockSynchronizer blockSynchronizer
        )
        {
            _blockManager = blockManager;
            _transactionManager = transactionManager;
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _keyGenRepository = keyGenRepository;
            _blockSynchronizer = blockSynchronizer;
            _blockManager.OnSystemContractInvoked += BlockManagerOnSystemContractInvoked;
        }

        private void BlockManagerOnSystemContractInvoked(object _, InvocationContext context)
        {
            if (context.Receipt is null) return;
            var highestBlock = _blockSynchronizer.GetHighestBlock();
            if (highestBlock.HasValue && highestBlock.Value > context.Receipt.Block)
            {
                if (!GovernanceContract.IsKeygenBlock(highestBlock.Value) ||
                    !GovernanceContract.SameCycle(highestBlock.Value, context.Receipt.Block))
                {
                    Logger.LogWarning(
                        $"Skipping keygen event since blockchain is already at height {highestBlock.Value} " +
                        $"and we are at {context.Receipt.Block}"
                    );
                    return;
                }
            }

            var tx = context.Receipt.Transaction;
            if (
                !tx.To.Equals(ContractRegisterer.GovernanceContract) &&
                !tx.To.Equals(ContractRegisterer.StakingContract)
            ) return;
            if (tx.Invocation.Length < 4) return;

            var signature = ContractEncoder.MethodSignatureAsInt(tx.Invocation);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            var contractAddress = tx.To;

            if (contractAddress.Equals(ContractRegisterer.GovernanceContract) && signature ==
                ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodFinishCycle))
            {
                Logger.LogDebug("Aborting ongoing keygen because cycle was finished");
                _keyGenRepository.SaveKeyGenState(Array.Empty<byte>());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(StakingInterface.MethodFinishVrfLottery))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodChangeValidators}");
                var data = new GovernanceContract(context).GetNextValidators();
                var publicKeys =
                    (data ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                Logger.LogTrace(
                    $"Keygen is started for validator set: {string.Join(",", publicKeys.Select(x => x.ToHex()))}"
                );
                if (!publicKeys.Contains(_privateWallet.EcdsaKeyPair.PublicKey))
                {
                    Logger.LogWarning("Skipping validator change event since we are not new validator");
                    return;
                }

                var keygen = GetCurrentKeyGen();
                if (keygen != null)
                    throw new ArgumentException("Cannot start keygen, since one is already running");
                var faulty = (publicKeys.Length - 1) / 3;
                keygen = new TrustlessKeygen(_privateWallet.EcdsaKeyPair, publicKeys, faulty);
                var commitTx = MakeCommitTransaction(keygen.StartKeygen());
                Logger.LogTrace($"Produced commit tx with hash: {commitTx.Hash.ToHex()}");
                if (_transactionPool.Add(commitTx) is var error && error != OperatingError.Ok)
                    Logger.LogError($"Error creating commit transaction ({commitTx.Hash.ToHex()}): {error}");
                else
                    Logger.LogInformation($"KeyGen Commit transaction sent");

                _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenCommit))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenCommit}");
                var keygen = GetCurrentKeyGen();
                if (keygen is null)
                {
                    Logger.LogWarning($"Skipping call since there is no keygen running, block is {highestBlock}, " + 
                                      $"stack trace is {new System.Diagnostics.StackTrace()}");
                    return;
                }

                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0)
                {
                    Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                    return;
                }

                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                var commitment = Commitment.FromBytes(args[0] as byte[] ?? throw new Exception());
                var encryptedRows = args[1] as byte[][] ?? throw new Exception();
                var sendValueTx = MakeSendValueTransaction(
                    keygen.HandleCommit(
                        sender,
                        new CommitMessage {Commitment = commitment, EncryptedRows = encryptedRows}
                    )
                );
                if (_transactionPool.Add(sendValueTx) is var error && error != OperatingError.Ok)
                    Logger.LogError($"Error creating send value transaction ({sendValueTx.Hash.ToHex()}): {error}");
                else
                    Logger.LogInformation($"KeyGen Send value transaction sent");

                _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenSendValue))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenSendValue}");
                var keygen = GetCurrentKeyGen();
                if (keygen is null) return;
                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;
                var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
                var proposer = args[0] as UInt256 ?? throw new Exception();
                var encryptedValues = args[1] as byte[][] ?? throw new Exception();

                Logger.LogDebug($"Send value tx invocation: {tx.Invocation}, proposer = {proposer.ToHex()}");
                if (keygen.HandleSendValue(
                    sender,
                    new ValueMessage {Proposer = (int) proposer.ToBigInteger(), EncryptedValues = encryptedValues}
                ))
                {
                    var keys = keygen.TryGetKeys() ?? throw new Exception();
                    var confirmTx = MakeConfirmTransaction(keys);
                    if (_transactionPool.Add(confirmTx) is var error && error != OperatingError.Ok)
                        Logger.LogError($"Error creating confirm transaction ({confirmTx.Hash.ToHex()}): {error}");
                    else
                        Logger.LogInformation($"KeyGen Confirm transaction sent");
                }

                _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenConfirm))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenConfirm}");
                var keygen = GetCurrentKeyGen();
                if (keygen is null) return;
                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey());
                if (sender < 0) return;

                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                var tpkePublicKey = PublicKey.FromBytes(args[0] as byte[] ?? throw new Exception());
                var tsKeys = new PublicKeySet(
                    (args[1] as byte[][] ?? throw new Exception()).Select(x =>
                        Crypto.ThresholdSignature.PublicKey.FromBytes(x)
                    ),
                    keygen.Faulty
                );

                if (keygen.HandleConfirm(tpkePublicKey, tsKeys))
                {
                    var keys = keygen.TryGetKeys() ?? throw new Exception();
                    Logger.LogTrace($"Generated keyring with public hash {keys.PublicPartHash().ToHex()}");
                    Logger.LogTrace($"  - TPKE public key: {keys.TpkePublicKey.ToHex()}");
                    Logger.LogTrace(
                        $"  - TS public key: {keys.ThresholdSignaturePrivateKey.GetPublicKeyShare().ToHex()}");
                    Logger.LogTrace(
                        $"  - TS public key set: {string.Join(", ", keys.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToHex()))}"
                    );
                    var lastBlockInCurrentCycle = (context.Receipt.Block / StakingContract.CycleDuration + 1) *
                                                  StakingContract.CycleDuration;
                    _privateWallet.AddThresholdSignatureKeyAfterBlock(
                        lastBlockInCurrentCycle, keys.ThresholdSignaturePrivateKey
                    );
                    _privateWallet.AddTpkePrivateKeyAfterBlock(lastBlockInCurrentCycle, keys.TpkePrivateKey);
                    Logger.LogDebug("New keyring saved to wallet");
                    _keyGenRepository.SaveKeyGenState(Array.Empty<byte>());
                }
                else
                {
                    _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
                }
            }
        }

        private TransactionReceipt MakeConfirmTransaction(ThresholdKeyring keyring)
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenConfirm,
                0,
                keyring.TpkePublicKey.ToBytes(),
                keyring.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray()
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TransactionReceipt MakeSendValueTransaction(ValueMessage valueMessage)
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenSendValue,
                0,
                new BigInteger(valueMessage.Proposer).ToUInt256(),
                valueMessage.EncryptedValues
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TransactionReceipt MakeCommitTransaction(CommitMessage commitMessage)
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenCommit,
                0,
                commitMessage.Commitment.ToBytes(),
                commitMessage.EncryptedRows
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TrustlessKeygen? GetCurrentKeyGen()
        {
            var bytes = _keyGenRepository.LoadKeyGenState();
            if (bytes is null || bytes.Length == 0) return null;
            return TrustlessKeygen.FromBytes(bytes, _privateWallet.EcdsaKeyPair);
        }

        public bool RescanBlockChainForKeys(IPublicConsensusKeySet publicKeysToSearch)
        {
            var keyRingHash = publicKeysToSearch.TpkePublicKey.ToBytes()
                .Concat(publicKeysToSearch.ThresholdSignaturePublicKeySet.ToBytes())
                .Keccak();
            var (from, to) = GetBlockRangeToRescanForKeys(keyRingHash);
            var keygen = new TrustlessKeygen(_privateWallet.EcdsaKeyPair, publicKeysToSearch.EcdsaPublicKeySet,
                publicKeysToSearch.F);
            for (var i = from; i <= to; ++i)
            {
                var block = _blockManager.GetByHeight(i) ??
                            throw new Exception($"No block {i} found but height is {_blockManager.GetHeight()}");
                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = _transactionManager.GetByHash(txHash) ??
                             throw new Exception($"Cannot find tx {txHash.ToHex()} included in block {i}");
                    if (!tx.Transaction.To.Equals(ContractRegisterer.GovernanceContract)) continue;
                    var signature = ContractEncoder.MethodSignatureAsInt(tx.Transaction.Invocation);
                    var decoder = new ContractDecoder(tx.Transaction.Invocation.ToArray());
                    if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenCommit))
                    {
                        Logger.LogDebug(
                            $"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenCommit}");
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey());
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                        var commitment = Commitment.FromBytes(args[0] as byte[] ?? throw new Exception());
                        var encryptedRows = args[1] as byte[][] ?? throw new Exception();
                        keygen.HandleCommit(
                            sender,
                            new CommitMessage {Commitment = commitment, EncryptedRows = encryptedRows}
                        );
                    }
                    else if (signature ==
                             ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenSendValue))
                    {
                        Logger.LogDebug(
                            $"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenSendValue}");
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey());
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
                        var proposer = args[0] as UInt256 ?? throw new Exception();
                        var encryptedValues = args[1] as byte[][] ?? throw new Exception();

                        keygen.HandleSendValue(
                            sender,
                            new ValueMessage
                                {Proposer = (int) proposer.ToBigInteger(), EncryptedValues = encryptedValues}
                        );
                    }
                    else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenConfirm))
                    {
                        Logger.LogDebug(
                            $"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenConfirm}");
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey());
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                        var tpkePublicKey = PublicKey.FromBytes(args[0] as byte[] ?? throw new Exception());
                        var tsKeys = new PublicKeySet(
                            (args[1] as byte[][] ?? throw new Exception()).Select(x =>
                                Crypto.ThresholdSignature.PublicKey.FromBytes(x)
                            ),
                            keygen.Faulty
                        );

                        if (!keygen.HandleConfirm(tpkePublicKey, tsKeys)) continue;
                        var keys = keygen.TryGetKeys() ?? throw new Exception();
                        Logger.LogTrace($"Generated keyring with public hash {keys.PublicPartHash().ToHex()}");
                        Logger.LogTrace($"  - TPKE public key: {keys.TpkePublicKey.ToHex()}");
                        Logger.LogTrace(
                            $"  - TS public key: {keys.ThresholdSignaturePrivateKey.GetPublicKeyShare().ToHex()}");
                        Logger.LogTrace(
                            $"  - TS public key set: {string.Join(", ", keys.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToHex()))}"
                        );
                        var lastBlockInCurrentCycle =
                            (i / StakingContract.CycleDuration + 1) * StakingContract.CycleDuration;
                        _privateWallet.AddThresholdSignatureKeyAfterBlock(
                            lastBlockInCurrentCycle, keys.ThresholdSignaturePrivateKey
                        );
                        _privateWallet.AddTpkePrivateKeyAfterBlock(lastBlockInCurrentCycle, keys.TpkePrivateKey);
                        Logger.LogDebug("New keyring saved to wallet");
                        return true;
                    }
                }
            }

            return false;
        }

        private (ulong, ulong) GetBlockRangeToRescanForKeys(UInt256 keyRingHash)
        {
            for (var i = _blockManager.GetHeight(); i > 0; --i)
            {
                var block = _blockManager.GetByHeight(i) ??
                            throw new Exception($"No block {i} found but height is {_blockManager.GetHeight()}");
                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = _transactionManager.GetByHash(txHash) ??
                             throw new Exception($"Cannot find tx {txHash.ToHex()} included in block {i}");
                    if (!tx.Transaction.To.Equals(ContractRegisterer.GovernanceContract)) continue;
                    if (!ContractEncoder.MethodSignature(GovernanceInterface.MethodKeygenConfirm).Take(4)
                        .SequenceEqual(tx.Transaction.Invocation.Take(4))) continue;
                    var decoder = new ContractDecoder(tx.Transaction.Invocation.ToArray());
                    var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                    var tpkeKey = args[0] as byte[] ??
                                  throw new Exception($"Cannot parse KeyGenConfirm tx {tx.Hash.ToHex()}");
                    var tsKeys = args[1] as byte[][] ??
                                 throw new Exception($"Cannot parse KeyGenConfirm tx {tx.Hash.ToHex()}");

                    var tsKeySet = new PublicKeySet(
                        tsKeys.Select(x => Lachain.Crypto.ThresholdSignature.PublicKey.FromBytes(x)),
                        (tsKeys.Length - 1) / 3
                    );
                    var confirmedHash = tpkeKey.Concat(tsKeySet.ToBytes()).Keccak();
                    if (!keyRingHash.Equals(confirmedHash)) continue;
                    Logger.LogDebug($"Found keygen confirmation in block {i} tx {tx.Hash.ToHex()}");
                    var start = i;
                    while (GovernanceContract.SameCycle(start, i)) --start;
                    return (start + 1, i);
                }
            }

            return (1, 0);
        }
    }
}