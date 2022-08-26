using System;
using System.Linq;
using System.Numerics;
using Lachain.Consensus;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
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
        private readonly ISystemContractReader _systemContractReader;
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
            IBlockSynchronizer blockSynchronizer,
            ISystemContractReader systemContractReader
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
            _systemContractReader = systemContractReader;
            _blockManager.OnSystemContractInvoked += BlockManagerOnSystemContractInvoked;
        }
        
        // For every cycle, a new set of keys are required for the validators. This key generation process
        // is done on-chain. That means, every communication between participating nodes happen via transactions
        // in the block. For example, if node A wants to send a msg to node B, then node A encrypts the 
        // msg with node B's public key and broadcast this as a transaction to the governance contract. 
        // After this transaction is added to the chain, node B can decrypt the msg and read it.

        // During block execution, after every system transaction is executed, the following method
        // is invoked. It evaluates the transaction and if it's keygen related, it produces
        // appropriate response in form of a transaction and adds it to the pool for the addition 
        // in the block.
        
        private void BlockManagerOnSystemContractInvoked(object _, InvocationContext context)
        {
            if (context.Receipt is null) return;
            var highestBlock = _blockSynchronizer.GetHighestBlock();
            var willParticipate =
                !highestBlock.HasValue ||
                GovernanceContract.IsKeygenBlock(context.Receipt.Block) &&
                GovernanceContract.SameCycle(highestBlock.Value, context.Receipt.Block);
            if (!willParticipate)
            {
                Logger.LogInformation(
                    highestBlock != null
                        ? $"Will not participate in keygen: highest block is {highestBlock.Value}, call block is {context.Receipt.Block}"
                        : $"Will not participate in keygen: highest block is null, call block is {context.Receipt.Block}"
                );
            }

            var tx = context.Receipt.Transaction;
            if (
                !tx.To.Equals(ContractRegisterer.GovernanceContract) &&
                !tx.To.Equals(ContractRegisterer.StakingContract)
            ) return;
            if (context.Receipt.Block < _blockManager.GetHeight() &&
                !GovernanceContract.SameCycle(context.Receipt.Block, _blockManager.GetHeight()))
            {
                Logger.LogWarning(
                    $"System contract invoked from outdated tx: {context.Receipt.Hash}, tx block {context.Receipt.Block}, our height is {_blockManager.GetHeight()}");
                return;
            }

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
                Logger.LogDebug($"Detected call of StakingInterface.{StakingInterface.MethodFinishVrfLottery}");
                var cycle = GovernanceContract.GetCycleByBlockNumber(context.Receipt.Block);
                var data = new GovernanceContract(context).GetNextValidators();
                var publicKeys =
                    (data ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                Logger.LogDebug(
                    $"Keygen is started in cycle={cycle}, block={context.Receipt.Block} for validator set: {string.Join(",", publicKeys.Select(x => x.ToHex()))}"
                );
                if (!publicKeys.Contains(_privateWallet.EcdsaKeyPair.PublicKey))
                {
                    Logger.LogWarning("Skipping validator change event since we are not new validator");
                    return;
                }

                var keygen = GetCurrentKeyGen();
                if (keygen != null && keygen.Cycle == cycle)
                {
                    throw new ArgumentException("Cannot start keygen, since one is already running");
                }

                if (keygen != null)
                {
                    Logger.LogWarning($"Aborted keygen for cycle {keygen.Cycle} to start keygen for cycle {cycle}");
                }

                _keyGenRepository.SaveKeyGenState(Array.Empty<byte>());

                var faulty = (publicKeys.Length - 1) / 3;
                keygen = new TrustlessKeygen(_privateWallet.EcdsaKeyPair, publicKeys, faulty, cycle);
                var commitTx = MakeCommitTransaction(keygen.StartKeygen(), cycle);
                Logger.LogTrace($"Produced commit tx with hash: {commitTx.Hash.ToHex()}");
                if (willParticipate)
                {
                    Logger.LogInformation($"Try to send KeyGen Commit transaction");
                    if (_transactionPool.Add(commitTx) is var error && error != OperatingError.Ok)
                        Logger.LogError($"Error creating commit transaction ({commitTx.Hash.ToHex()}): {error}");
                    else
                        Logger.LogInformation($"KeyGen Commit transaction sent");
                }

                Logger.LogDebug($"Saving keygen {keygen.ToBytes().ToHex()}");
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

                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(context.Receipt.Block)));
                if (sender < 0)
                {
                    Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                    return;
                }

                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                var cycle = args[0] as UInt256 ?? throw new Exception("Failed to get cycle for SendValue transaction");
                var commitment = Commitment.FromBytes(args[1] as byte[] ??
                                                      throw new Exception(
                                                          "Failed to get commitment for SendValue transaction"));
                var encryptedRows = args[2] as byte[][] ??
                                    throw new Exception("Failed to get encryptedRows for SendValue transaction");
                
                if (cycle.ToBigInteger() != keygen.Cycle)
                {
                    Logger.LogError($"Got KeygenCommit for cycle {cycle.ToBigInteger()} while doing keygen for {keygen.Cycle}");
                    return;
                }
                
                var sendValueTx = MakeSendValueTransaction(
                    cycle,
                    keygen.HandleCommit(
                        sender,
                        new CommitMessage {Commitment = commitment, EncryptedRows = encryptedRows}
                    )
                );
                if (willParticipate)
                {
                    if (_transactionPool.Add(sendValueTx) is var error && error != OperatingError.Ok)
                        Logger.LogError($"Error creating send value transaction ({sendValueTx.Hash.ToHex()}): {error}");
                    else
                        Logger.LogInformation($"KeyGen Send value transaction sent");
                }

                _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenSendValue))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenSendValue}");
                var keygen = GetCurrentKeyGen();
                if (keygen is null) return;
                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(context.Receipt.Block)));
                if (sender < 0) 
                {
                    Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                    return;
                }

                var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
                var cycle = args[0] as UInt256 ?? throw new Exception("Failed to get cycle for Confirm transaction");
                var proposer = args[1] as UInt256 ??
                               throw new Exception("Failed to get proposer for Confirm transaction");
                var encryptedValues = args[2] as byte[][] ??
                                      throw new Exception("Failed to get encryptedValues for Confirm transaction");
                if (cycle.ToBigInteger() != keygen.Cycle)
                {
                    Logger.LogError($"Got KeygenSendValue for cycle {cycle.ToBigInteger()} while doing keygen for {keygen.Cycle}");
                    return;
                }
                
                Logger.LogTrace($"Send value tx invocation: {tx.Invocation.ToHex()}, proposer = {proposer.ToHex()}");
                if (keygen.HandleSendValue(
                    sender,
                    new ValueMessage {Proposer = (int) proposer.ToBigInteger(), EncryptedValues = encryptedValues}
                ))
                {
                    var keys = keygen.TryGetKeys() ?? throw new Exception();
                    var confirmTx = MakeConfirmTransaction(cycle, keys);

                    if (willParticipate)
                    {
                        if (_transactionPool.Add(confirmTx) is var error && error != OperatingError.Ok)
                            Logger.LogError($"Error creating confirm transaction ({confirmTx.Hash.ToHex()}): {error}");
                        else
                            Logger.LogInformation($"KeyGen Confirm transaction sent");
                    }
                }

                _keyGenRepository.SaveKeyGenState(keygen.ToBytes());
            }
            else if (signature == ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodKeygenConfirm))
            {
                Logger.LogDebug($"Detected call of GovernanceContract.{GovernanceInterface.MethodKeygenConfirm}");
                var keygen = GetCurrentKeyGen();
                if (keygen is null) return;
                var sender = keygen.GetSenderByPublicKey(context.Receipt.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(context.Receipt.Block)));
                if (sender < 0) 
                {
                    Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                    return;
                }

                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                var cycle = args[0] as UInt256 ?? throw new Exception("Failed to get cycle");
                var tpkePublicKey =
                    PublicKey.FromBytes(args[1] as byte[] ?? throw new Exception("Failed to get tpkePublicKey"));
                var tsKeys = new PublicKeySet(
                    (args[3] as byte[][] ?? throw new Exception()).Select(x =>
                        Crypto.ThresholdSignature.PublicKey.FromBytes(x)
                    ),
                    keygen.Faulty
                );
                if (cycle.ToBigInteger() != keygen.Cycle)
                {
                    Logger.LogError($"Got KeygenConfirm for cycle {cycle.ToBigInteger()} while doing keygen for {keygen.Cycle}");
                    return;
                }

                if (keygen.HandleConfirm(tpkePublicKey, tsKeys))
                {
                    var keys = keygen.TryGetKeys() ?? throw new Exception();
                    Logger.LogTrace($"Generated keyring with public hash {keys.PublicPartHash().ToHex()}");
                    Logger.LogTrace($"  - TPKE public key: {keys.TpkePublicKey.ToHex()}");
                    Logger.LogTrace(
                        $"  - TPKE verification public keys: {string.Join(", ", keys.TpkeVerificationPublicKeys.Select(key => key.ToHex()))}"
                    );
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

        private TransactionReceipt MakeConfirmTransaction(UInt256 cycle, ThresholdKeyring keyring)
        {
            Logger.LogTrace("MakeConfirmTransaction");
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenConfirm,
                0,
                cycle,
                keyring.TpkePublicKey.ToBytes(),
                keyring.TpkeVerificationPublicKeys.Select(key => key.ToBytes()).ToArray(), 
                keyring.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray()
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
        }

        private TransactionReceipt MakeSendValueTransaction(UInt256 cycle, ValueMessage valueMessage)
        {
            Logger.LogTrace("MakeSendValueTransaction");
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenSendValue,
                0,
                cycle,
                new BigInteger(valueMessage.Proposer).ToUInt256(),
                valueMessage.EncryptedValues
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
        }

        private TransactionReceipt MakeCommitTransaction(CommitMessage commitMessage, ulong cycle)
        {
            Logger.LogTrace("MakeCommitTransaction");
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenCommit,
                0,
                new BigInteger(cycle).ToUInt256(),
                commitMessage.Commitment.ToBytes(),
                commitMessage.EncryptedRows
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
        }

        private TrustlessKeygen? GetCurrentKeyGen()
        {
            var bytes = _keyGenRepository.LoadKeyGenState();
            if (bytes is null || bytes.Length == 0) return null;
            return TrustlessKeygen.FromBytes(bytes, _privateWallet.EcdsaKeyPair);
        }

        public bool RescanBlockChainForKeys(IPublicConsensusKeySet publicKeysToSearch)
        {
            var (from, to) = GetBlockRangeToRescanForKeys();
            var keygen = new TrustlessKeygen(
                _privateWallet.EcdsaKeyPair,
                publicKeysToSearch.EcdsaPublicKeySet,
                publicKeysToSearch.F,
                0
            );
            Logger.LogDebug($"Searching for keygen transactions in blocks [{from}; {to}]");
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
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(i)));
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
                        var commitment = Commitment.FromBytes(args[1] as byte[] ?? throw new Exception());
                        var encryptedRows = args[2] as byte[][] ?? throw new Exception();
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
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(i)));
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
                        var proposer = args[1] as UInt256 ?? throw new Exception();
                        var encryptedValues = args[2] as byte[][] ?? throw new Exception();

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
                        var sender = keygen.GetSenderByPublicKey(tx.RecoverPublicKey(HardforkHeights.IsHardfork_9Active(i)));
                        if (sender < 0)
                        {
                            Logger.LogWarning($"Skipping call because of invalid sender: {sender}");
                            continue;
                        }

                        var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
                        var tpkePublicKey = PublicKey.FromBytes(args[1] as byte[] ?? throw new Exception());
                        var tsKeys = new PublicKeySet(
                            (args[2] as byte[][] ?? throw new Exception()).Select(x =>
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

        private (ulong, ulong) GetBlockRangeToRescanForKeys()
        {
            var end = _systemContractReader.GetLastSuccessfulKeygenBlock();
            var start = end;
            while (GovernanceContract.SameCycle(start, end))
            {
                if (start == 0) return (0, end);
                --start;
            }

            return (start + 1, end);
        }
    }
}