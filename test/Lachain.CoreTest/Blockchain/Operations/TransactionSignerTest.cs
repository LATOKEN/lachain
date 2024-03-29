using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using Google.Protobuf;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Networking;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.Operations
{
    public class SignerTest
    {

        private IConfigManager _configManager = null!;
        private IContainer? _container;

        [OneTimeSetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            containerBuilder.RegisterModule<ConfigModule>();
            _container = containerBuilder.Build();
            _configManager = _container.Resolve<IConfigManager>();
            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_SignAndRecover()
        {
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            var signer = new TransactionSigner();
            var abi = ContractEncoder.Encode("hello()");
            var tx = new Transaction
            {
                To = ContractRegisterer.LatokenContract,
                Invocation = ByteString.CopyFrom(abi),
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = 123123,
                /* TODO: "calculate gas limit for input size" */
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = 0,
                Value = new BigInteger(0).ToUInt256()
            };
            // using old chain id
            var receipt = signer.Sign(tx, keyPair, false);
            Assert.AreEqual(receipt.Hash.ToHex(), receipt.FullHash(false).ToHex());

            var publicKey = receipt.RecoverPublicKey(false);
            Assert.AreEqual(keyPair.PublicKey.ToHex(), publicKey.ToHex());
            Assert.AreEqual(keyPair.PublicKey.GetAddress().ToHex(), publicKey.GetAddress().ToHex());

            // using new chain id
            receipt = signer.Sign(tx, keyPair, true);
            Assert.AreEqual(receipt.Hash.ToHex(), receipt.FullHash(true).ToHex());

            publicKey = receipt.RecoverPublicKey(true);
            Assert.AreEqual(keyPair.PublicKey.ToHex(), publicKey.ToHex());
            Assert.AreEqual(keyPair.PublicKey.GetAddress().ToHex(), publicKey.GetAddress().ToHex());
        }
        
        [Test]
        public void Test_SignIssue()
        {
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var signer = new TransactionSigner();
            var data =
                "0x0061736d01000000011c0660017f006000017f60037f7f7f0060027f7f0060000060017f017f0290010703656e76156765745f7472616e736665727265645f66756e6473000003656e760d6765745f63616c6c5f73697a65000103656e760f636f70795f63616c6c5f76616c7565000203656e760c6c6f61645f73746f72616765000303656e760a7365745f72657475726e000303656e760b73797374656d5f68616c74000003656e760c736176655f73746f72616765000303060504050202040405017001010105030100020608017f01418080040b071202066d656d6f72790200057374617274000b0ab806052e004100410036028080044100410036028480044100410036028c800441003f0041107441f0ff7b6a36028880040ba60101047f418080042101024003400240200128020c0d002001280208220220004f0d020b200128020022010d000b41002101410028020821020b02402002200041076a41787122036b22024118490d00200120036a41106a22002001280200220436020002402004450d00200420003602040b2000200241706a3602082000410036020c2000200136020420012000360200200120033602080b2001410136020c200141106a0b2d002000411f6a21000340200120002d00003a0000200141016a21012000417f6a21002002417f6a22020d000b0b2d002001411f6a21010340200120002d00003a00002001417f6a2101200041016a21002002417f6a22020d000b0b820402047f047e230041a0016b2200240020001000024002400240024002402000290300200041106a29030084200041086a290300200041186a29030084844200520d00100741001001220136020441002001100822023602084100200120021002200141034d0d014100200228020022033602000240200341eea3f68405460d00200341d4c78168470d0220004180016a41186a420037030020004200370390012000420037038801200042003703800120004180016a200041e0006a1003200041206a41186a200041e0006a41186a2903003703002000200041f0006a2903003703302000200041e8006a290300370328200020002903603703204100450d0341004100100441011005000b2001417c6a4120490d03200241046a200041c0006a41201009200041c8006a2903002104200041d0006a2903002105200041c0006a41186a29030021062000290340210720004180016a41186a4200370300200041e0006a41186a200637030020004200370390012000420037038801200042003703800120002005370370200020043703682000200737036020004180016a200041e0006a100641010d0441004100100441011005000b41004100100441011005000b41004100100441011005000b200041206a4120100822004120100a20004120100441001005000b200041a0016a240041030f0b41004100100441001005000b00740970726f647563657273010c70726f6365737365642d62790105636c616e675431302e302e3120286769743a2f2f6769746875622e636f6d2f6c6c766d2f6c6c766d2d70726f6a65637420623661313733343336373838653638333239636335653965653066363531623630336136333765332900ad01046e616d6501a5010c00156765745f7472616e736665727265645f66756e6473010d6765745f63616c6c5f73697a65020f636f70795f63616c6c5f76616c7565030c6c6f61645f73746f72616765040a7365745f72657475726e050b73797374656d5f68616c74060c736176655f73746f72616765070b5f5f696e69745f6865617008085f5f6d616c6c6f63090b5f5f62653332746f6c654e0a0b5f5f6c654e746f626533320b057374617274"
                    .HexToBytes();
            var tx = new Transaction
            {
                To = UInt160Utils.Empty,
                Invocation = ByteString.CopyFrom(data),
                From = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes().ToUInt160(),
                GasPrice = 1,
                GasLimit = 5369560,
                Nonce = 0,
                Value = new BigInteger(0).ToUInt256()
            };
            // using old chain id
            var receipt = signer.Sign(tx, keyPair, false);
            Assert.AreEqual(receipt.Hash.ToHex(), receipt.FullHash(false).ToHex());
            var txHashFromWeb3Py = "0x0bd482bdd02f75f4897658f39c6ddf0a4ef0c58b2f4c3acdf0474ba497a0a6d5";
            Assert.AreEqual(receipt.Hash.ToHex(), txHashFromWeb3Py);

            var publicKey = receipt.RecoverPublicKey(false);
            Assert.AreEqual(keyPair.PublicKey.ToHex(), publicKey.ToHex());
            Assert.AreEqual(keyPair.PublicKey.GetAddress().ToHex(), publicKey.GetAddress().ToHex());

            
            // using new chain id
            receipt = signer.Sign(tx, keyPair, true);
            Assert.AreEqual(receipt.Hash.ToHex(), receipt.FullHash(true).ToHex());
            txHashFromWeb3Py = "0x45361b6213f2f7115feb7044ab9f52e03f2c7e49bb1866b5d094a0e39f0fa2f6";
            Assert.AreEqual(receipt.Hash.ToHex(), txHashFromWeb3Py);

            publicKey = receipt.RecoverPublicKey(true);
            Assert.AreEqual(keyPair.PublicKey.ToHex(), publicKey.ToHex());
            Assert.AreEqual(keyPair.PublicKey.GetAddress().ToHex(), publicKey.GetAddress().ToHex());
            
        }
    }
}