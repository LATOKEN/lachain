using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using Nethereum.Signer;
using NUnit.Framework;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Operations;
using Lachain.Crypto.Misc;
using Lachain.Utility;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Google.Protobuf;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Networking;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Serialization;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3TransactionTests
    {
        private IContainer? _container;
        private IStateManager? _stateManager;
        private ITransactionManager? _transactionManager;
        private ITransactionBuilder? _transactionBuilder;
        private ITransactionSigner? _transactionSigner;
        private ITransactionPool? _transactionPool;
        private IContractRegisterer? _contractRegisterer;
        private IPrivateWallet? _privateWallet;
        private IConfigManager _configManager = null!;

        private TransactionServiceWeb3? _apiService = null;

        [SetUp]
        public void Setup()
        {
            
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            
            _container = containerBuilder.Build();

            _stateManager = _container.Resolve<IStateManager>();
            _transactionManager = _container.Resolve<ITransactionManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            
            ServiceBinder.BindService<GenericParameterAttributes>();
            _apiService = new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionBuilder, _transactionSigner,
                _transactionPool, _contractRegisterer, _privateWallet);
            _configManager = _container.Resolve<IConfigManager>();
            if (TransactionUtils.ChainId == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!);
            }

        }

        [TearDown]
        public void Teardown()
        {
            var sessionId = Handler.DefaultSessionId();
            Handler.DestroySession(sessionId);

            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }


        [Test]
        [Ignore("fix it")]
        public void Test_SendRawTransactionSimpleSend()
        {
            var rawTx2 = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var ethTx = new TransactionChainId(rawTx2.HexToBytes());
            var t = _apiService!.MakeTransaction(ethTx);
            
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(t, keyPair);
            var rawtx2 = TransactionUtils.RlpWithSignature(receipt.Transaction, receipt.Signature).ToHex();
            Assert.AreEqual(rawTx2, rawtx2);

            var txid = _apiService!.SendRawTransaction(rawTx2);
            // check we get a transaction hash,  not error message
            Assert.AreEqual("0x", txid.Substring(0, 2));
            // check this hash is not empty
            Assert.AreNotEqual("0x", txid);
        }


        [Test]
        [Ignore("fix it")]
        public void Test_SendRawTransactionContractInvocation()
        {   
            var rawTx2 = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var ethTx = new TransactionChainId(rawTx2.HexToBytes());
            var t = _apiService!.MakeTransaction(ethTx);

            var r = ethTx.Signature.R;
            while (r.Length < 32)
                r = "00".HexToBytes().Concat(r).ToArray();

            var s = ethTx.Signature.S;
            while (s.Length < 32)
                s = "00".HexToBytes().Concat(s).ToArray();

            var signature = r.Concat(s).Concat(ethTx.Signature.V).ToArray();

            var keyPair = new EcdsaKeyPair("0xE83385AF76B2B1997326B567461FB73DD9C27EAB9E1E86D26779F4650C5F2B75"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(t, keyPair);
            Assert.AreEqual(receipt.Signature, signature.ToSignature());

            var ethTx2 = t.GetEthTx(receipt.Signature);
            Assert.AreEqual(ethTx.ChainId, ethTx2.ChainId);
            Assert.AreEqual(ethTx.Data, ethTx2.Data);
            //            Assert.AreEqual(ethTx.Nonce,  ethTx2.Nonce);
            Assert.AreEqual(ethTx.Signature.R, ethTx2.Signature.R);
            Assert.AreEqual(ethTx.Signature.S, ethTx2.Signature.S);
            Assert.AreEqual(ethTx.Signature.V, ethTx2.Signature.V);
            //            Assert.AreEqual(ethTx.Value,  ethTx2.Value);
            Assert.AreEqual(ethTx.GasLimit, ethTx2.GasLimit);
            Assert.AreEqual(ethTx.GasPrice, ethTx2.GasPrice);
            Assert.AreEqual(ethTx.ReceiveAddress, ethTx2.ReceiveAddress);
            //Assert.AreEqual(ethTx,  ethTx2);

            var txid = _apiService!.SendRawTransaction(rawTx2);
            // check we get a transaction hash,  not error message
            Assert.AreEqual("0x", txid.Substring(0, 2));
            // check this hash is not empty
            Assert.AreNotEqual("0x", txid);

            // check encoding is correct
            var decoder = new ContractDecoder(t.Invocation.ToByteArray());
            var args = decoder.Decode(Lrc20Interface.MethodSetAllowedSupply);
            var res = args[0] as UInt256 ?? throw new Exception("Failed to decode invocation");
            var supply = new BigInteger(5001000) * BigInteger.Pow(10, 18); ;
            Console.WriteLine($"supply: {supply}");
            Assert.AreEqual(res.ToHex(), supply.ToUInt256().ToHex());
        }
        
    }
}
