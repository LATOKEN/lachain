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
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.SystemContracts
{
    public class StakingContractTest
    {
        private readonly IContainer _container;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public StakingContractTest()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }
        
        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container.Dispose();
        }
        
        [Test]
        public void Test_OneNodeCycle()
        {
            var stateManager = _container.Resolve<IStateManager>();
            var contractRegisterer = _container.Resolve<IContractRegisterer>();
            var tx = new TransactionReceipt();
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);
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
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.GovernanceContract, input);
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
    }
}