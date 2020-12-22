using System.Collections.Generic;
using System.Linq;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class Web3DataFormatUtils
    {
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

        public static string Web3Data(UInt256 value)
        {
            return Web3Data(value.Buffer);
        }

        public static string Web3Data(UInt160 value)
        {
            return Web3Data(value.Buffer);
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

        public static JObject Web3Transaction(
            TransactionReceipt receipt,
            UInt256? blockHash = null,
            ulong? blockNumber = null
        )
        {
            // var logs = new JArray();
            // var eventCount = stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(receipt.Hash);
            // for (var i = (uint) 0; i < eventCount; i++)
            // {
            //     var eventLog = stateManager.LastApprovedSnapshot.Events
            //         .GetEventByTransactionHashAndIndex(receipt.Hash, i)!;
            //     ExtractDataAndTopics(eventLog.Data.ToByteArray(), out var topics, out var data);
            //     var log = new JObject
            //     {
            //         ["address"] = eventLog.Contract.ToHex(),
            //         ["topics"] = topics,
            //         ["data"] = data.ToHex(true),
            //         ["blockNumber"] = blockNumber ?? receipt.Block.ToHex(),
            //         ["transactionHash"] = receipt.Hash.ToHex(),
            //         ["blockHash"] = blockHash ?? block?.Hash.ToHex(),
            //         ["logIndex"] = 0,
            //         ["removed"] = false,
            //     };
            //     logs.Add(log);
            // }

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
                ["contractAddress"] = null, // TODO: contract address
                ["logs"] = logs,
                ["logsBloom"] = Web3Data(Enumerable.Repeat<byte>(0, 256)), // empty bloom filter
                ["status"] = Web3Number(receipt.Status == TransactionStatus.Executed ? 1ul : 0ul),
            };
        }
    }
}