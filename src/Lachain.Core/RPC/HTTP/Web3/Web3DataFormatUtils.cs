using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;
using Lachain.Storage.Trie;
using Lachain.Storage;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class Web3DataFormatUtils
    {
        private static readonly ILogger<Web3DataFormatUtils> Logger =
            LoggerFactory.GetLoggerForClass<Web3DataFormatUtils>();
        
        // Reference: https://eth.wiki/json-rpc/API

        /**
         * Number implies shortest possible hex representation
         */
        public static string Web3Number(ulong x)
        {
            return x.ToHex(false);
        }

        public static string Web3Number(UInt256 x)
        {
            return x.ToBigInteger().ToHex(false);
        }

        /**
         * Data implies full hex encoding byte by byte not trimming anything
         */
        public static string Web3Data(IEnumerable<byte> bytes)
        {
            return bytes.ToHex();
        }

        public static string Web3Data(UInt256? value)
        {
            return value == null ? "0x" : Web3Data(value.Buffer);
        }

        public static string Web3Data(UInt160? value)
        {
            return value == null ? "0x" : Web3Data(value.Buffer);
        }

        public static string Web3Data(ulong nonce)
        {
            return Web3Data(nonce.ToBytes());
        }

        public static JObject Web3Block(Block block, ulong gasUsed, JArray txs)
        {
            return new JObject
            {
                ["number"] = Web3Number(block.Header.Index),
                ["hash"] = Web3Data(block.Hash),
                ["parentHash"] = Web3Data(block.Header.PrevBlockHash),
                ["nonce"] = Web3Data(block.Header.Nonce),
                ["sha3Uncles"] = Web3Data(new byte[] {0xc0}.Keccak()), // 0xc0 is RLP of empty list
                ["logsBloom"] = Web3Data(Enumerable.Repeat<byte>(0, 256)), // empty bloom filter
                ["transactionsRoot"] = Web3Data(block.Header.MerkleRoot),
                ["stateRoot"] = Web3Data(block.Header.StateHash),
                ["receiptsRoot"] = Web3Data(Enumerable.Repeat<byte>(0, 32)), // not supported
                ["miner"] = Web3Data(ContractRegisterer.StakingContract),
                ["difficulty"] = Web3Number(0),
                ["totalDifficulty"] = Web3Number(0),
                ["extraData"] = Web3Data(Enumerable.Empty<byte>()),
                ["size"] = Web3Number((ulong) block.CalculateSize()),
                ["gasLimit"] = Web3Number(GasMetering.DefaultBlockGasLimit),
                ["gasUsed"] = Web3Number(gasUsed),
                ["timestamp"] = Web3Number(block.Timestamp / 1000),
                ["transactions"] = txs,
                ["uncles"] = new JArray(),
            };
        }

        public static JObject Web3Trie(IDictionary<ulong,IHashTrieNode> dict)
        {
            var jobject = new JObject{};

            foreach(var item in dict)
            {
                jobject[Web3Number(item.Key)] = Web3DataFormatUtils.Web3Node(item.Value, item.Key);
            }
            return jobject;
        }

        public static JObject Web3Node(IHashTrieNode node, ulong root)
        {
            switch (node)
            {
                case InternalNode internalNode:
                    var jArray = new JArray();
                    foreach(var item in node.Children) jArray.Add(Web3Number(item));
                    return new JObject{
                        ["NodeType"] = Web3Number(1),
                        ["Hash"] = Web3Data(internalNode.Hash.ToUInt256()),
                        ["ChildrenMask"] = Web3Number((ulong)internalNode.ChildrenMask),
                        ["Children"] = jArray,
                    };     


                case LeafNode leafNode:
                    return new JObject{
                        ["NodeType"] = Web3Number(2),
                        ["Hash"] = Web3Data(leafNode.Hash.ToUInt256()),
                        ["KeyHash"] = Web3Data(leafNode.KeyHash.ToUInt256()),
                        ["Value"] = Web3Data(leafNode.Value),
                    };
            }
            return new JObject{};
        }

        public static UInt64 GetUInt64FromHex(string hex)
        { 
            return Convert.ToUInt64(hex, 16);
        }

        public static IDictionary<ulong, IHashTrieNode> TrieFromJson(JObject trieJson)
        {
            IDictionary<ulong, IHashTrieNode> allNodes = new Dictionary<ulong, IHashTrieNode>();
            foreach(var item in trieJson)
            {
                ulong version = Convert.ToUInt64( ((string)item.Key), 16);
                allNodes[version] = NodeFromJsonObject( (JObject)item.Value);
            }
            return allNodes;
        }

        public static IHashTrieNode NodeFromJsonObject(JObject nodeJson)
        {
            if (((string)nodeJson["NodeType"]).Equals("0x1") == true)
            {
                uint mask = Convert.ToUInt32((string)nodeJson["ChildrenMask"], 16);
                byte[] hash = HexUtils.HexToBytes((string)nodeJson["Hash"]);
                var childrenJson = (JArray)nodeJson["Children"];

                List<ulong> children = new List<ulong>();
                foreach(var childJson in childrenJson)
                {
                    children.Add(Convert.ToUInt64((string)childJson, 16));
                }

                return new InternalNode(mask, children, hash);
            }
            else{
                byte[] keyHash = HexUtils.HexToBytes((string)nodeJson["KeyHash"]);
                byte[] value = HexUtils.HexToBytes((string)nodeJson["Value"]);
                return new LeafNode(keyHash, value);
            }
        }

        public static JObject Web3Transaction(
            TransactionReceipt receipt,
            UInt256? blockHash = null,
            ulong? blockNumber = null
        )
        {
            var signature = receipt.Signature.Encode();
            return new JObject
            {
                ["blockHash"] = blockHash != null ? Web3Data(blockHash) : null,
                ["blockNumber"] = blockNumber != null ? Web3Number(blockNumber.Value) : null,
                ["from"] = Web3Data(receipt.Transaction.From),
                ["gas"] = Web3Number(receipt.Transaction.GasLimit),
                ["gasPrice"] = Web3Number(receipt.Transaction.GasPrice),
                ["hash"] = Web3Data(receipt.Hash),
                ["input"] = Web3Data(receipt.Transaction.Invocation),
                ["nonce"] = Web3Number(receipt.Transaction.Nonce),
                ["to"] = receipt.Transaction.To.Buffer.IsEmpty ? null : Web3Data(receipt.Transaction.To),
                ["transactionIndex"] = Web3Number(receipt.IndexInBlock),
                ["value"] = Web3Number(receipt.Transaction.Value),
                ["r"] = Web3Data(signature.Take(32)),
                ["s"] = Web3Data(signature.Skip(32).Take(32)),
                ["v"] = Web3Number(signature[64]),
            };
        }

        public static JArray Web3BlockTransactionArray(
            IEnumerable<TransactionReceipt> txs,
            UInt256? blockHash = null,
            ulong? blockNumber = null
        )
        {
            var logs = new JArray();
            foreach(TransactionReceipt tx in txs)
                logs.Add(Web3Transaction(tx, blockHash, blockNumber));
            return logs;
        }

        public static JObject Web3Event(Event e, ulong? blockNumber = null)
        {
            if (e.Contract is null || e.Data is null || e.TransactionHash is null || e.BlockHash is null)
            {
                Logger.LogWarning($"event lacks one of important fields: {e}");
            }
            return new JObject
            {
                ["address"] = Web3Data(e.Contract),
                ["topics"] = new JArray(), // we don't support indexes
                ["data"] = Web3Data(e.Data ?? Enumerable.Empty<byte>()),
                ["blockNumber"] = Web3Number(blockNumber ?? 0),
                ["transactionHash"] = Web3Data(e.TransactionHash),
                ["blockHash"] = Web3Data(e.BlockHash),
                ["logIndex"] = Web3Number(e.Index),
                ["transactionIndex"] = Web3Number(0),
                ["removed"] = "false",
            };
        }

        public static JArray Web3EventArray(IEnumerable<Event> events, ulong? blockNumber = null)
        {
            var logs = new JArray();
            foreach(Event e in events)
                logs.Add(Web3Event(e, blockNumber));
            return logs;
        }
        
        public static JObject Web3TransactionReceipt(
            TransactionReceipt receipt, UInt256 blockHash, ulong blockNumber, ulong cumulativeGasUsed, JArray logs
        )
        {
            return new JObject
            {
                ["transactionHash"] = Web3Data(receipt.Hash),
                ["transactionIndex"] = Web3Number(receipt.IndexInBlock),
                ["blockHash"] = Web3Data(blockHash),
                ["blockNumber"] = Web3Number(blockNumber),
                ["from"] = Web3Data(receipt.Transaction.From),
                ["to"] = receipt.Transaction.To.Buffer.IsEmpty ? null : Web3Data(receipt.Transaction.To),
                ["cumulativeGasUsed"] = Web3Number(cumulativeGasUsed),
                ["gasUsed"] = Web3Number(receipt.GasUsed),
                ["contractAddress"] = GetContractAddress(receipt),
                ["logs"] = logs,
                ["logsBloom"] = Web3Data(Enumerable.Repeat<byte>(0, 256)), // empty bloom filter
                ["status"] = Web3Number(receipt.Status == TransactionStatus.Executed ? 1ul : 0ul),
            };
        }

        private static string? GetContractAddress(TransactionReceipt receipt)
        {
            if (!receipt.Transaction.To.Buffer.IsEmpty && !receipt.Transaction.To.IsZero())
                return null;
            return Web3Data(receipt.Transaction.From.ToBytes().Concat(receipt.Transaction.Nonce.ToBytes()).Ripemd());
        }
    }
}

