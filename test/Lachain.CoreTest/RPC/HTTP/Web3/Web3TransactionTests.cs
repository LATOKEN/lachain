using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Error;
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

using Lachain.Core.Blockchain.Operations;
using Lachain.Crypto.Misc;
using Lachain.Utility;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Google.Protobuf;
using System.Collections.Generic;

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

        private TransactionServiceWeb3? _apiService;

        // from BlockTest.cs
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new Core.Blockchain.Operations.TransactionSigner();
        private IBlockManager _blockManager = null!;
        //private IPrivateWallet _privateWallet = null!;

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

            // from BlockTest.cs
            _blockManager = _container.Resolve<IBlockManager>();
            //_privateWallet = _container.Resolve<IPrivateWallet>();

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
        public void Test_SendRawTransactionSimpleSend()
        {
            var rawTx2 = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var ethTx = new TransactionChainId(rawTx2.HexToBytes());
            var t = _apiService!.MakeTransaction(ethTx);
            
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(t, keyPair);

            var txid = _apiService!.SendRawTransaction(rawTx2);
            Assert.AreEqual("0x", txid.Substring(0, 2));
            Assert.AreNotEqual("0x", txid);
        }
        

        [Test]
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
            Assert.AreEqual(ethTx.ChainId,  ethTx2.ChainId);
            Assert.AreEqual(ethTx.Data,  ethTx2.Data);
//            Assert.AreEqual(ethTx.Nonce,  ethTx2.Nonce);
            Assert.AreEqual(ethTx.Signature.R,  ethTx2.Signature.R);
            Assert.AreEqual(ethTx.Signature.S,  ethTx2.Signature.S);
            Assert.AreEqual(ethTx.Signature.V,  ethTx2.Signature.V);
//            Assert.AreEqual(ethTx.Value,  ethTx2.Value);
            Assert.AreEqual(ethTx.GasLimit,  ethTx2.GasLimit);
            Assert.AreEqual(ethTx.GasPrice,  ethTx2.GasPrice);
            Assert.AreEqual(ethTx.ReceiveAddress,  ethTx2.ReceiveAddress);
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
            var supply = new BigInteger(5001000) * BigInteger.Pow(10, 18);;
            Console.WriteLine($"supply: {supply}");
            Assert.AreEqual(res.ToHex(), supply.ToUInt256().ToHex());
        }


        [Test]
        //changed VerifyRawTransaction from private to public
        public void Test_VerifyRawTransaction_Valid_txn()
        {
            var rawTx = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";

            var result = _apiService!.VerifyRawTransaction(rawTx);

            Assert.AreEqual(result, "0x2ad6261b4d33fc9d55eed4c48f16e33aba6178a8359c33237dba240b4f20aafb");
        }

        [Test]
        //changed GetTransactionReceipt from private to public
        public void Test_GetTransactionReceipt()
        {
            _blockManager.TryBuildGenesisBlock();
            GenerateBlocks(1);

            var rawTx = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";

            var ethTx = new TransactionChainId(rawTx.HexToBytes());

            var txHashSent = _apiService!.SendRawTransaction(rawTx);

            Console.WriteLine($"tx sent: {txHashSent}");

            var tx = new TransactionReceipt();
            var sender = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            var tsnapshot = _stateManager.LastApprovedSnapshot;

            var context = new InvocationContext(sender, _stateManager.LastApprovedSnapshot, tx);
            context.Snapshot.Balances.SetBalance(sender, Money.Parse("90000000000000000"));

            var balance = context.Snapshot.Balances.GetBalance(sender);

            Console.WriteLine($"Account Balance: {balance}");

            GenerateBlocks(1);

            var txReceipt = _apiService!.GetTransactionReceipt(txHashSent);

            var txHashReceived = txReceipt["transactionHash"].ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());


        }

        [Test]
        /// Changed from private to public
        public void Test_GetNetworkGasPrice()
        {
            var gasPrice_Expected = "0x0";

            var gasPrice_Actual = _apiService!.GetNetworkGasPrice();

            Assert.AreEqual(gasPrice_Expected, gasPrice_Actual);

        }

        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 0; i < blockNum; i++)
            {
                var txes = GetCurrentPoolTxes();
                var block = BuildNextBlock(txes);
                var result = ExecuteBlock(block, txes);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes()
        {
            return _transactionPool.Peek(1000, 1000).ToArray();
        }


        public void AddDummuyTx()
        {
            _blockManager.TryBuildGenesisBlock();
            var topUpReceipts = new List<TransactionReceipt>();
            var randomReceipts = new List<TransactionReceipt>();
            var txCount = new Random().Next(1, 50);

            var coverTxFeeAmount = Money.Parse("10.0");
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransaction();
                randomReceipts.Add(tx);
                topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                    (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i));
            }

            var topUpBlock = BuildNextBlock(topUpReceipts.ToArray());
            var topUpResult = ExecuteBlock(topUpBlock, topUpReceipts.ToArray());
            Assert.AreEqual(topUpResult, OperatingError.Ok);

            var randomBlock = BuildNextBlock(randomReceipts.ToArray());
            var result = ExecuteBlock(randomBlock, randomReceipts.ToArray());
            Assert.AreEqual(result, OperatingError.Ok);

            var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(randomBlock.Header.Index);
            Assert.AreEqual(executedBlock!.TransactionHashes.Count, txCount);

        }

        // from BlockTest.cs
        private Block BuildNextBlock(TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(merkleRoot, predecessor, _stateManager.LastApprovedSnapshot.StateHash);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = { receipts.Select(tx => tx.Hash) },
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(UInt256 merkleRoot, Block? predecessor,
            UInt256 stateHash)
        {
            var blockIndex = predecessor!.Header.Index + 1;
            var header = new BlockHeader
            {
                Index = blockIndex,
                PrevBlockHash = predecessor!.Hash,
                MerkleRoot = merkleRoot,
                StateHash = stateHash,
                Nonce = blockIndex
            };

            var keyPair = _privateWallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = { _privateWallet.EcdsaKeyPair.PublicKey },
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _privateWallet.EcdsaKeyPair.PublicKey,
                        Value = headerSignature,
                    }
                }
            };
            return (header, multisig);
        }

        private OperatingError ExecuteBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            Console.WriteLine($"Executed block: {block.Header.Index}");
            return status;
        }

        private OperatingError EmulateBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };
            var (status, _, _, _) = _blockManager.Emulate(block, receipts);
            return status;
        }

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc)
        {
            var temptp = _transactionPool;

            var tx = new Proto.Transaction
            {
                To = to,
                From = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                GasPrice = (ulong)Money.Parse("0.0000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(_privateWallet.EcdsaKeyPair.PublicKey.GetAddress()) +
                        (ulong)nonceInc,
                Value = value
            };
            return Signer.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        private TransactionReceipt ApproveTx(UInt160 to, UInt256 value, int nonceInc)
        {
            var input = ContractEncoder.Encode(Lrc20Interface.MethodApprove, to, value);
            var tx = new Proto.Transaction
            {
                To = ContractRegisterer.LatokenContract,
                Invocation = ByteString.CopyFrom(input),
                From = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                GasPrice = (ulong)Money.Parse("0.0000001").ToWei(),
                GasLimit = 10_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(_privateWallet.EcdsaKeyPair.PublicKey.GetAddress()) +
                        (ulong)nonceInc,
                Value = UInt256Utils.Zero,
            };
            return Signer.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

    }
}