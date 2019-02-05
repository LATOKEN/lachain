using System;
using Newtonsoft.Json.Linq;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.JSON
{
    public static class TransactionJsonConverter
    {
        public static JObject ToJson(this ContractTransaction contractTransaction)
        {
            var json = new JObject
            {
                ["asset"] = contractTransaction.Asset.Buffer.ToHex(),
                ["to"] = contractTransaction.To.Buffer.ToHex(),
                ["value"] = contractTransaction.Value.Buffer.ToHex(),
                ["input"] = contractTransaction.Input?.ToHex()
            };
            return json;
        }

        public static JObject ToJson(this IssueTransaction issueTransaction)
        {
            var json = new JObject
            {
                ["asset"] = issueTransaction.Asset.Buffer.ToHex(),
                ["supply"] = issueTransaction.Supply.Buffer.ToHex(),
                ["to"] = issueTransaction.To.Buffer.ToHex()
            };
            return json;
        }

        public static JObject ToJson(this RegisterTransaction registerTransaction)
        {
            var json = new JObject
            {
                ["type"] = registerTransaction.Type.ToString(),
                ["name"] = registerTransaction.Name,
                ["supply"] = registerTransaction.Supply?.Buffer.ToHex(),
                ["decimals"] = registerTransaction.Decimals,
                ["owner"] = registerTransaction.Owner?.Buffer.ToHex(),
                ["minter"] = registerTransaction.Minter?.Buffer.ToHex()
            };
            return json;
        }

        public static JObject ToJson(this DepositTransaction depositTransaction)
        {
            var json = new JObject
            {
                ["recipient"] = depositTransaction.Recipient.Buffer.ToHex(),
                ["blockchainType"] = depositTransaction.BlockchainType.ToString(),
                ["value"] = depositTransaction.Value.Buffer.ToHex(),
                ["transactionHash"] = depositTransaction.TransactionHash.ToHex(),
                ["addressFormat"] = depositTransaction.AddressFormat.ToString(),
                ["timestamp"] = depositTransaction.Timestamp,
                ["assetHash"] = depositTransaction.AssetHash.Buffer.ToHex()
            };
            return json;
        }
        
        public static JObject ToJson(this WithdrawTransaction withdrawTransaction)
        {
            var json = new JObject
            {
                ["recipient"] = withdrawTransaction.Recipient.Buffer.ToHex(),
                ["blockchainType"] = withdrawTransaction.BlockchainType.ToString(),
                ["value"] = withdrawTransaction.Value.Buffer.ToHex(),
                ["transactionHash"] = withdrawTransaction.TransactionHash.ToHex(),
                ["addressFormat"] = withdrawTransaction.AddressFormat.ToString(),
                ["timestamp"] = withdrawTransaction.Timestamp,
                ["assetHash"] = withdrawTransaction.AssetHash.Buffer.ToHex()
            };
            return json;
        }
        
        public static JObject ToJson(this ConfirmTransaction confirmTransaction)
        {
            var json = new JObject
            {
                ["recipient"] = confirmTransaction.Recipient.Buffer.ToHex(),
                ["blockchainType"] = confirmTransaction.BlockchainType.ToString(),
                ["value"] = confirmTransaction.Value.Buffer.ToHex(),
                ["transactionHash"] = confirmTransaction.TransactionHash.ToHex(),
                ["addressFormat"] = confirmTransaction.AddressFormat.ToString(),
                ["timestamp"] = confirmTransaction.Timestamp,
                ["assetHash"] = confirmTransaction.AssetHash.Buffer.ToHex()
            };
            return json;
        }

        public static JObject ToJson(this DeployTransaction deployTransaction)
        {
            var json = new JObject
            {
                ["wasm"] = deployTransaction.Wasm.ToHex(),
                ["version"] = deployTransaction.Version.ToString()
            };
            return json;
        }

        public static JObject ToJson(this Transaction transaction)
        {
            var json = new JObject
            {
                ["type"] = transaction.Type.ToString(),
                ["from"] = transaction.From?.Buffer?.ToHex(),
                ["nonce"] = transaction.Nonce,
                ["fee"] = transaction.Fee?.Buffer?.ToHex()
            };
            switch (transaction.Type)
            {
                case TransactionType.Miner:
                    /* We don't support this transaction type anymore */
                    break;
                case TransactionType.Register:
                    json["register"] = transaction.Register.ToJson();
                    break;
                case TransactionType.Issue:
                    json["issue"] = transaction.Issue.ToJson();
                    break;
                case TransactionType.Contract:
                    json["contract"] = transaction.Contract.ToJson();
                    break;
                case TransactionType.Deposit:
                    json["deposit"] = transaction.Deposit.ToJson();
                    break;
                case TransactionType.Withdraw:
                    json["withdraw"] = transaction.Withdraw.ToJson();
                    break;
                case TransactionType.Confirm:
                    json["confirm"] = transaction.Confirm.ToJson();
                    break;
                case TransactionType.Deploy:
                    json["deploy"] = transaction.Deploy.ToJson();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transaction));
            }
            return json;
        }
        
        public static JObject ToJson(this AcceptedTransaction acceptedTransaction)
        {
            var json = new JObject
            {
                ["transaction"] = acceptedTransaction.Transaction?.ToJson(),
                ["hash"] = acceptedTransaction.Hash?.Buffer?.ToHex(),
                ["signature"] = acceptedTransaction.Signature?.Buffer?.ToHex(),
                ["block"] = acceptedTransaction.Block?.Buffer?.ToHex(),
                ["status"] = acceptedTransaction.Status.ToString()
            };
            return json;
        }
    }
}