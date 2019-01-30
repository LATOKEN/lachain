﻿using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionBuilder : ITransactionBuilder
    {
        private readonly IStateManager _stateManager;
        
        public TransactionBuilder(IStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public Transaction RegisterTransaction(AssetType type, string name, Money supply, uint decimals, UInt160 owner)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var registerTx = new RegisterTransaction
            {
                Type = type,
                Name = name,
                Supply = supply.ToUInt256(),
                Decimals = decimals,
                Owner = owner
            };
            var tx = new Transaction
            {
                Type = TransactionType.Register,
                From = UInt160Utils.Zero,
                Register = registerTx,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            return tx;
        }

        public Transaction ContractTransaction(UInt160 from, UInt160 to, Asset asset, Money value, byte[] input)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var contractTx = new ContractTransaction
            {
                Asset = asset.Hash,
                To = to,
                Value = value.ToUInt256(),
            };
            if (input != null)
                contractTx.Input = ByteString.CopyFrom(input);
            var tx = new Transaction
            {
                Type = TransactionType.Contract,
                From = from,
                Contract = contractTx,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            return tx;
        }

        public Transaction DeployTransaction(UInt160 from, IEnumerable<ContractABI> abi, IEnumerable<byte> wasm,
            ContractVersion version)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var deployTx = new DeployTransaction
            {
                Version = ContractVersion.Wasm,
                Wasm = ByteString.CopyFrom(wasm.ToArray())
            };
            var tx = new Transaction
            {
                Type = TransactionType.Deploy,
                From = from,
                Deploy = deployTx,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            return tx;
        }

        public Transaction TransferTransaction(UInt160 from, UInt160 to, string assetName, Money value)
        {
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(assetName);
            if (asset is null)
                throw new Exception($"Unable to resolve asset by name ({assetName})");
            return TransferTransaction(from, to, asset.Hash, value);
        }

        public Transaction TransferTransaction(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var contractTx = new ContractTransaction
            {
                Asset = asset,
                To = to,
                Value = value.ToUInt256()
            };
            var tx = new Transaction
            {
                Type = TransactionType.Contract,
                From = from,
                Contract = contractTx,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            return tx;
        }

        public Transaction DepositTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var deposit = new DepositTransaction
            {
                Recipient = recipient,
                BlockchainType = blockchainType,
                Value = value.ToUInt256(),
                AddressFormat = addressFormat,
                Timestamp = timestamp,
                TransactionHash = ByteString.CopyFrom(transactionHash)
            };
            var tx = new Transaction
            {
                Type = TransactionType.Deposit,
                From = from,
                Nonce = nonce,
                Fee = _CalcEstimatedBlockFee(),
                Deposit = deposit
            };
            return tx;
        }

        public Transaction WithdrawTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value, byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var withdraw = new WithdrawTransaction
            {
                Recipient = recipient,
                BlockchainType = blockchainType,
                Value = value.ToUInt256(),
                AddressFormat = addressFormat,
                Timestamp = timestamp,
                TransactionHash = ByteString.CopyFrom(transactionHash)
            };
            var tx = new Transaction
            {
                Type = TransactionType.Withdraw,
                From = from,
                Nonce = nonce,
                Fee = _CalcEstimatedBlockFee(),
                Withdraw = withdraw
            };
            return tx;
        }

        public Transaction ConfirmTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value, byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var confirm = new ConfirmTransaction
            {
                Recipient = recipient,
                BlockchainType = blockchainType,
                Value = value.ToUInt256(),
                AddressFormat = addressFormat,
                Timestamp = timestamp,
                TransactionHash = ByteString.CopyFrom(transactionHash)
            };
            var tx = new Transaction
            {
                Type = TransactionType.Deposit,
                From = from,
                Nonce = nonce,
                Fee = _CalcEstimatedBlockFee(),
                Confirm = confirm
            };
            return tx;
        }

        private UInt256 _CalcEstimatedBlockFee()
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(
                _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight());
            return block is null ? UInt256Utils.Zero : _CalcEstimatedBlockFee(block.TransactionHashes).ToUInt256();
        }
        
        private Money _CalcEstimatedBlockFee(IEnumerable<UInt256> txHashes)
        {
            var arrayOfHashes = txHashes as UInt256[] ?? txHashes.ToArray();
            if (arrayOfHashes.Length == 0)
                return Money.Zero;
            var sum = Money.Zero;
            foreach (var txHash in arrayOfHashes)
            {
                var tx = _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(txHash);
                if (tx is null)
                    continue;
                sum += tx.Transaction.Fee.ToMoney();
            }
            return sum / arrayOfHashes.Length;
        }
    }
}