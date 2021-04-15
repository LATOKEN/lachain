using System;
using System.Linq;
using System.Numerics;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Proto;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3DataFormatUtilsTest
    {
        [Test]
        public void Test_Web3Number()
        {
            Assert.AreEqual("0x0", Web3DataFormatUtils.Web3Number(0));
            Assert.AreEqual("0x1", Web3DataFormatUtils.Web3Number(1));
            Assert.AreEqual("0xa", Web3DataFormatUtils.Web3Number(10));
            Assert.AreEqual("0x10", Web3DataFormatUtils.Web3Number(16));
            Assert.AreEqual("0x41", Web3DataFormatUtils.Web3Number(65));
            Assert.AreEqual("0xff", Web3DataFormatUtils.Web3Number(255));
            Assert.AreEqual("0x400", Web3DataFormatUtils.Web3Number(1024));

            Assert.AreEqual("0x0", Web3DataFormatUtils.Web3Number(UInt256Utils.Zero));
            Assert.AreEqual("0x1", Web3DataFormatUtils.Web3Number(BigInteger.Parse("1").ToUInt256()));
            Assert.AreEqual("0xa", Web3DataFormatUtils.Web3Number(BigInteger.Parse("10").ToUInt256()));
            Assert.AreEqual("0x10", Web3DataFormatUtils.Web3Number(BigInteger.Parse("16").ToUInt256()));
            Assert.AreEqual("0x41", Web3DataFormatUtils.Web3Number(BigInteger.Parse("65").ToUInt256()));
            Assert.AreEqual("0xff", Web3DataFormatUtils.Web3Number(BigInteger.Parse("255").ToUInt256()));
            Assert.AreEqual("0x400", Web3DataFormatUtils.Web3Number(BigInteger.Parse("1024").ToUInt256()));
            // 2 ** 252 - 1
            Assert.AreEqual(
                "0xfffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                Web3DataFormatUtils.Web3Number(
                    BigInteger
                        .Parse("7237005577332262213973186563042994240829374041602535252466099000494570602495")
                        .ToUInt256()
                )
            );
            // 2 ** 256 - 1
            Assert.AreEqual(
                "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                Web3DataFormatUtils.Web3Number(
                    BigInteger
                        .Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935")
                        .ToUInt256()
                )
            );
        }

        [Test]
        public void Test_Web3Data()
        {
            Assert.AreEqual("0x", Web3DataFormatUtils.Web3Data(Enumerable.Empty<byte>()));
            Assert.AreEqual("0x41", Web3DataFormatUtils.Web3Data(new byte[] {65}));
            Assert.AreEqual("0x004200", Web3DataFormatUtils.Web3Data(new byte[] {0, 66, 0}));
            Assert.AreEqual("0x0f0f0f", Web3DataFormatUtils.Web3Data(new byte[] {15, 15, 15}));

            Assert.AreEqual(
                "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                Web3DataFormatUtils.Web3Data(new byte[] {0xc0}.Keccak())
            );
            Assert.AreEqual(
                "0x0100000000000000000000000000000000000000000000000000000000000000",
                Web3DataFormatUtils.Web3Data(BigInteger.One.ToUInt256())
            );
            Assert.AreEqual(
                "0x0300000000000000000000000000000000000000",
                Web3DataFormatUtils.Web3Data(ContractRegisterer.StakingContract)
            );
            var address = "023f7d80bc1c1f2a93bca97e81b9f3073150e15cef78b8d37a7ec4c947993ad5e7"
                .HexToBytes()
                .ToPublicKey()
                .GetAddress();
            Assert.AreEqual("0xa4aee5f4599fa96cfca74f339c0544635e70d152", Web3DataFormatUtils.Web3Data(address));
        }

        [Test]
        public void Test_Web3Block()
        {
            // Console.WriteLine(MerkleTree.ComputeRoot(new UInt256[]{})?.ToHex());
            // var block = new Block()
            // {
            //     Header = new BlockHeader
            //     {
            //         Index = 436,
            //         MerkleRoot = MerkleTree.ComputeRoot(new UInt256[] { }),
            //
            //     },
            //     Timestamp = 1438271100,
            //
            // };
            // {
            //     "difficulty": "0x0",
            //     "extraData": "0x",
            //     "gasLimit": "0x174876e800",
            //     "gasUsed": "0x0",
            //     "hash": "0xdc0818cf78f21a8e70579cb46a43643f78291264dda342ae31049421c82d21ae",
            //     "logsBloom": "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
            //     "miner": "0xbb7b8287f3f0a933474a79eae42cbca977791171",
            //     "mixHash": "0x4fffe9ae21f1c9e15207b1f472d5bbdd68c9595d461666602f2be20daf5e7843",
            //     "nonce": "0x689056015818adbe",
            //     "number": "0x1b4",
            //     "parentHash": "0xe99e022112df268087ea7eafaf4790497fd21dbeeb6bd7a1721df161a6657a54",
            //     "receiptsRoot": "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421",
            //     "sha3Uncles": "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
            //     "size": "0x220",
            //     "stateRoot": "0xddc8b0234c2e0cad087c8b389aa7ef01f7d79b2570bccb77ce48648aa61c904d",
            //     "timestamp": "0x55ba467c",
            //     "totalDifficulty": "0x78ed983323d",
            //     "transactions": [
            //     ],
            //     "transactionsRoot": "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421",
            //     "uncles": [
            //     ]
            // }
            // Web3DataFormatUtils.Web3Block()   
        }
       
        //
        // public static JObject Web3Transaction(
        //     IStateManager stateManager,
        //     TransactionReceipt receipt,
        //     string? blockHash = null,
        //     string? blockNumber = null,
        //     Block? block = null,
        //     bool isReceipt = false
        // )
        // {
        //     var logs = new JArray();
        //     var eventCount = stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(receipt.Hash);
        //     for (var i = (uint) 0; i < eventCount; i++)
        //     {
        //         var eventLog = stateManager.LastApprovedSnapshot.Events
        //             .GetEventByTransactionHashAndIndex(receipt.Hash, i)!;
        //         ExtractDataAndTopics(eventLog.Data.ToByteArray(), out var topics, out var data);
        //         var log = new JObject
        //         {
        //             ["address"] = eventLog.Contract.ToHex(),
        //             ["topics"] = topics,
        //             ["data"] = data.ToHex(true),
        //             ["blockNumber"] = blockNumber ?? receipt.Block.ToHex(),
        //             ["transactionHash"] = receipt.Hash.ToHex(),
        //             ["blockHash"] = blockHash ?? block?.Hash.ToHex(),
        //             ["logIndex"] = 0,
        //             ["removed"] = false,
        //         };
        //         logs.Add(log);
        //     }
        //
        //     var res = new JObject
        //     {
        //         ["transactionHash"] = receipt.Hash.ToHex(),
        //         ["transactionIndex"] = receipt.IndexInBlock.ToHex(),
        //         ["blockNumber"] = blockNumber ?? receipt.Block.ToHex(),
        //         ["blockHash"] = blockHash ?? block?.Hash.ToHex(),
        //         ["cumulativeGasUsed"] = receipt.GasUsed.ToBytes().Reverse().ToHex(), // TODO: plus previous
        //         ["gasUsed"] = receipt.GasUsed.ToBytes().Reverse().ToHex(),
        //         ["contractAddress"] = null,
        //         ["logs"] = logs,
        //         ["logsBloom"] =
        //             "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        //         ["status"] = receipt.Status.CompareTo(TransactionStatus.Executed) == 0 ? "0x1" : "0x0",
        //         ["r"] = receipt.Signature.Encode().Take(32).ToHex(),
        //         ["s"] = receipt.Signature.Encode().Skip(32).Take(32).ToHex(),
        //         ["v"] = receipt.Signature.Encode().Skip(64).ToHex(),
        //         ["gas"] = receipt.Transaction.GasLimit.ToHex(),
        //         ["to"] = receipt.Transaction.To.ToHex(),
        //         ["from"] = receipt.Transaction.From.ToHex(),
        //     };
        //     if (!isReceipt)
        //     {
        //         res["value"] = receipt.Transaction.Value.ToBytes().Reverse().SkipWhile(x => x == 0).ToHex();
        //         res["nonce"] = receipt.Transaction.Nonce.ToHex();
        //         res["input"] = receipt.Transaction.Invocation.ToHex();
        //         res["hash"] = receipt.Hash.ToHex();
        //         res["gasPrice"] = receipt.Transaction.GasPrice.ToHex();
        //     }
        //
        //     return res;
        // }
    }
}