using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.Network
{
    
    public class MessageHandlerTest
    {
        private IContainer _container = null!;
        private MessageHandler _handler = null!;
        private IStateManager _stateManager = null!;

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            _container = containerBuilder.Build();
            _handler = (MessageHandler) _container.Resolve<IMessageHandler>();
            _stateManager = _container.Resolve<IStateManager>();
        }
        
        
        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void SyncBlockRequestTest()
        {
            var snap = _stateManager.NewSnapshot().Blocks;
            ulong myHeight = 20;
            var blocks = new List<Block>();
            for (ulong i = 0; i <= myHeight; i++)  {
                var block = new Block
                {
                    Header = new BlockHeader
                    {
                        Index = i
                    },
                    Hash = TestUtils.GetRandomBytes(32).ToUInt256(),
                    Multisig = new MultiSig
                    {
                        Validators = {},
                        Signatures = {}
                    }
                };
                snap.AddBlock(block);
                blocks.Add(block);
            }
            _stateManager.Approve();

            var request = new SyncBlocksRequest();
            request.FromHeight = 1;
            request.ToHeight = 10;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, _stateManager.LastApprovedSnapshot.Blocks));
            
            request.FromHeight = 0;
            request.ToHeight = 30;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, _stateManager.LastApprovedSnapshot.Blocks));

            
            request.FromHeight = 0;
            request.ToHeight = 1000000;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, _stateManager.LastApprovedSnapshot.Blocks));
            
            request.FromHeight = 10;
            request.ToHeight = 0;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, _stateManager.LastApprovedSnapshot.Blocks));

            var newReq = new SyncBlocksRequest
            {
                FromHeight = 10,
                ToHeight = 19,
                Proof = 
                {
                    blocks.Take(10).Select(
                        block => new BlockInfo
                        {
                            Block = block,
                            Transactions = 
                            {
                                block.TransactionHashes.Select(
                                    tx => _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx)
                                )
                            }
                        }
                    )
                }
            };
            Assert.DoesNotThrow(() => _handler.ValidateSyncBlocksRequest(newReq, _stateManager.LastApprovedSnapshot.Blocks));
        }
        
        [Test]
        public void SyncPoolRequestTest()
        {
            var request = new SyncPoolRequest();
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolRequest(request));

            request.Hashes.Add(new UInt256());
            Assert.DoesNotThrow(() => _handler.ValidateSyncPoolRequest(request));
            
            for (int i=0; i<10009; i++)
                request.Hashes.Add(new UInt256());
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolRequest(request));
        }

        [Test]
        public void SyncPoolReplyTest()
        {
            var reply = new SyncPoolReply();
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolReply(reply));
            
            reply.Transactions.Add(new TransactionReceipt());
            Assert.DoesNotThrow(() => _handler.ValidateSyncPoolReply(reply));

            for (int i = 0; i < 10000; i++)
            {
                reply.Transactions.Add(new TransactionReceipt());                
            }
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolReply(reply));
        }
    }
}