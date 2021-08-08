// using System;

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Containers;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NLog.Fluent;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.SystemContracts
{
    public class GovernanceContractTest
    {
        private IContainer? _container;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
            
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_OneNodeCycle()
        {
            var stateManager = _container?.Resolve<IStateManager>();
            var contractRegisterer = _container?.Resolve<IContractRegisterer>();
            var tx = new TransactionReceipt();
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager!.LastApprovedSnapshot, tx);
            var contract = new GovernanceContract(context);
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] pubKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            ECDSAPublicKey[] allKeys = {keyPair.PublicKey};
            var keygen = new TrustlessKeygen(keyPair, allKeys, 0, 0);
            var cycle = 0.ToUInt256();
            ValueMessage value;
            
            // call ChangeValidators method
            {
                byte[][] validators = {pubKey};
                var input = ContractEncoder.Encode(GovernanceInterface.MethodChangeValidators, cycle, validators);
                var call = contractRegisterer!.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.ChangeValidators(cycle, validators, frame));
            }
            // check correct validator 
            {
                var input = ContractEncoder.Encode(GovernanceInterface.MethodIsNextValidator, pubKey);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsNextValidator(pubKey, frame));
                Assert.AreEqual(frame.ReturnValue, 1.ToUInt256().ToBytes());
            }
            // check incorrect validator 
            {
                byte[] incorrectPubKey = pubKey.Reverse().ToArray();
                var input = ContractEncoder.Encode(GovernanceInterface.MethodIsNextValidator, incorrectPubKey);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsNextValidator(incorrectPubKey, frame));
                Assert.AreEqual(frame.ReturnValue, 0.ToUInt256().ToBytes());
            }
            // call commit
            {
                var commitMessage = keygen.StartKeygen();
                byte[] commitment = commitMessage.Commitment.ToBytes();
                byte[][] encryptedRows = commitMessage.EncryptedRows;
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenCommit, cycle, commitment, encryptedRows);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenCommit(cycle, commitment, encryptedRows, frame));
                // several calls is ok
                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenCommit(cycle, commitment, encryptedRows, frame));
                // set keygen state
                value = keygen.HandleCommit(0, commitMessage);
            }
            // send value
            {
                var proposer = new BigInteger(0).ToUInt256();
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenSendValue, cycle, proposer, value.EncryptedValues);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenSendValue(cycle, proposer, value.EncryptedValues, frame));
                // set keygen state
                Assert.IsTrue(keygen.HandleSendValue(0, value));
                Assert.IsTrue(keygen.Finished());
            }
            // confirm
            {
                ThresholdKeyring? keyring = keygen.TryGetKeys();
                Assert.IsNotNull(keyring);
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenConfirm, cycle, 
                    keyring!.Value.TpkePublicKey.ToBytes(), 
                    keyring!.Value.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray());
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenConfirm(cycle, keyring!.Value.TpkePublicKey.ToBytes(), 
                    keyring!.Value.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray(), frame));
                // set keygen state
                Assert.IsTrue(keygen.HandleConfirm(keyring!.Value.TpkePublicKey,  
                    keyring!.Value.ThresholdSignaturePublicKeySet));
            }
            // check no validators in storage
            Assert.Throws<ConsensusStateNotPresentException>(()=>context.Snapshot.Validators.GetValidatorsPublicKeys());
            // finish cycle
            {
                var input = ContractEncoder.Encode(GovernanceInterface.MethodFinishCycle, cycle);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                // should fail due to the invalid block
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.FinishCycle(cycle, frame));
                // set next cycle block number in frame:
                frame.InvocationContext.Receipt.Block = 20;
                Assert.AreEqual(ExecutionStatus.Ok, contract.FinishCycle(cycle, frame));
            }
            // check new validators in storage
            var newValidators = context.Snapshot.Validators.GetValidatorsPublicKeys().ToArray();
            Assert.AreEqual(newValidators.Count(), 1);
            Assert.AreEqual(newValidators[0], keyPair.PublicKey);
        }

        [Test]
        public void Test_InvalidValidatorKey()
        {
            var stateManager = _container?.Resolve<IStateManager>();
            var contractRegisterer = _container?.Resolve<IContractRegisterer>();
            var tx = new TransactionReceipt();
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager!.LastApprovedSnapshot, tx);
            var contract = new GovernanceContract(context);
            var keyPair = new EcdsaKeyPair(Crypto.GeneratePrivateKey().ToPrivateKey());
            
            ECDSAPublicKey[] allKeys = {keyPair.PublicKey};
            var keygen = new TrustlessKeygen(keyPair, allKeys, 0, 0);
            var cycle = 0.ToUInt256();
            ValueMessage value;
            
            // call ChangeValidators method with invalid key
            {
                byte[][] validators = {new byte[] {0}};
                var input = ContractEncoder.Encode(GovernanceInterface.MethodChangeValidators, cycle, validators);
                var call = contractRegisterer!.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.ChangeValidators(cycle, validators, frame));
            }
            
            // call commit
            {
                var commitMessage = keygen.StartKeygen();
                byte[] commitment = commitMessage.Commitment.ToBytes();
                byte[][] encryptedRows = commitMessage.EncryptedRows;
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenCommit, cycle, commitment, encryptedRows);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.KeyGenCommit(cycle, commitment, encryptedRows, frame));
                // set keygen state
                value = keygen.HandleCommit(0, commitMessage);
            }
            
            // send value
            {
                var proposer = new BigInteger(0).ToUInt256();
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenSendValue, cycle, proposer, value.EncryptedValues);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.KeyGenSendValue(cycle, proposer, value.EncryptedValues, frame));
                // set keygen state
                Assert.IsTrue(keygen.HandleSendValue(0, value));
                Assert.IsTrue(keygen.Finished());
            }
            
            // confirm
            {
                ThresholdKeyring? keyring = keygen.TryGetKeys();
                Assert.IsNotNull(keyring);
                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenConfirm, cycle, 
                    keyring!.Value.TpkePublicKey.ToBytes(), 
                    keyring!.Value.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray());
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenConfirm(cycle, keyring!.Value.TpkePublicKey.ToBytes(), 
                    keyring!.Value.ThresholdSignaturePublicKeySet.Keys.Select(key => key.ToBytes()).ToArray(), frame));
                // set keygen state
                Assert.IsTrue(keygen.HandleConfirm(keyring!.Value.TpkePublicKey,  
                    keyring!.Value.ThresholdSignaturePublicKeySet));
            }
            
            // check no validators in storage
            Assert.Throws<ConsensusStateNotPresentException>(()=>context.Snapshot.Validators.GetValidatorsPublicKeys());
            
            // finish cycle
            {
                var input = ContractEncoder.Encode(GovernanceInterface.MethodFinishCycle, cycle);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                // set next cycle block number in frame:
                frame.InvocationContext.Receipt.Block = 20;
                Assert.AreEqual(ExecutionStatus.Ok, contract.FinishCycle(cycle, frame));
            }
            
            // check no validators in storage again
            Assert.IsEmpty(context.Snapshot.Validators.GetValidatorsPublicKeys());
        }

        private class QueueItem
        {
            public int sender;
            public object? payload;

            public QueueItem(int sender, object? payload)
            {
                this.payload = payload;
                this.sender = sender;
            }
        }
        private void ExecuteCycle(int n, int f)
        {
            var stateManager = _container?.Resolve<IStateManager>();
            var contractRegisterer = _container?.Resolve<IContractRegisterer>();
            var tx = new TransactionReceipt();
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager!.LastApprovedSnapshot, tx);
            var contract = new GovernanceContract(context);
            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(_ => Crypto.GeneratePrivateKey())
                .Select(x => x.ToPrivateKey())
                .Select(x => new EcdsaKeyPair(x))
                .ToArray();
            var pubKeys = ecdsaKeys.Select(x => CryptoUtils.EncodeCompressed(x.PublicKey)).ToArray();
            var keyGens = Enumerable.Range(0, n)
                .Select(i => new TrustlessKeygen(ecdsaKeys[i], ecdsaKeys.Select(x => x.PublicKey), f, 0))
                .ToArray();
            var cycle = 0.ToUInt256();

            var messageLedger = new RandomSamplingQueue<QueueItem>();
            messageLedger.Enqueue(new QueueItem(-1, null));
            
            // call ChangeValidators method
            {
                byte[][] validators = pubKeys;
                var input = ContractEncoder.Encode(GovernanceInterface.MethodChangeValidators, cycle, validators);
                var call = contractRegisterer!.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.ChangeValidators(cycle, validators, frame));
            }
            // check correct validator 
            {
                for (var i = 0; i < n; i++)
                {
                    var input = ContractEncoder.Encode(GovernanceInterface.MethodIsNextValidator, pubKeys[i]);
                    var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                    Assert.IsNotNull(call);
                    var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                    Assert.AreEqual(ExecutionStatus.Ok, contract.IsNextValidator(pubKeys[i], frame));
                    Assert.AreEqual(frame.ReturnValue, 1.ToUInt256().ToBytes());
                }
            }
            // check incorrect validator 
            {
                byte[] incorrectPubKey = pubKeys[0].Reverse().ToArray();
                var input = ContractEncoder.Encode(GovernanceInterface.MethodIsNextValidator, incorrectPubKey);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsNextValidator(incorrectPubKey, frame));
                Assert.AreEqual(frame.ReturnValue, 0.ToUInt256().ToBytes());
            }

            while (messageLedger.Count > 0)
            {
                QueueItem? msg;
                var success = messageLedger.TryDequeue(out msg);
                Assert.IsTrue(success);
                switch (msg.payload)
                {
                    case null:
                        for (var i = 0; i < n; ++i)
                            messageLedger.Enqueue(new QueueItem(i, keyGens[i].StartKeygen()));
                        break;
                    case CommitMessage commitMessage:
                        for (var i = 0; i < n; ++i)
                        {
                            if (i == 0)
                            {
                                byte[] commitment = commitMessage.Commitment.ToBytes();
                                byte[][] encryptedRows = commitMessage.EncryptedRows;
                                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenCommit, cycle, commitment,
                                    encryptedRows);
                                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                                Assert.IsNotNull(call);
                                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenCommit(cycle, commitment, encryptedRows, frame));
                                // several calls is ok
                                Assert.AreEqual(ExecutionStatus.Ok, contract.KeyGenCommit(cycle, commitment, encryptedRows, frame));
                            }
                            messageLedger.Enqueue(new QueueItem(i, keyGens[i].HandleCommit(msg.sender, commitMessage)));
                        }
                        break;
                    case ValueMessage valueMessage:
                        for (var i = 0; i < n; ++i)
                        {
                            if (i == 0)
                            {
                                var proposer = new BigInteger(msg.sender).ToUInt256();
                                var input = ContractEncoder.Encode(GovernanceInterface.MethodKeygenSendValue,
                                    cycle, proposer, valueMessage.EncryptedValues);
                                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract,
                                    input);
                                Assert.IsNotNull(call);
                                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                                Assert.AreEqual(ExecutionStatus.Ok,
                                    contract.KeyGenSendValue(cycle, proposer, valueMessage.EncryptedValues,
                                        frame));
                            }
                            keyGens[i].HandleSendValue(msg.sender, valueMessage);
                        }
                        break;
                    default:
                        Assert.Fail($"Message of type {msg.GetType()} occurred");
                        break;
                }
            }
            Assert.IsTrue(keyGens.All((x) => x.Finished()));
            
            // finish cycle
            {
                var input = ContractEncoder.Encode(GovernanceInterface.MethodFinishCycle, cycle);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                // set next cycle block number in frame:
                frame.InvocationContext.Receipt.Block = 20;
                Assert.AreEqual(ExecutionStatus.Ok, contract.FinishCycle(cycle, frame));
            }
        }
        
        [Test]
        public void Test_FourNodeCycle()
        {
            ExecuteCycle(4, 1);
        }
    }
}