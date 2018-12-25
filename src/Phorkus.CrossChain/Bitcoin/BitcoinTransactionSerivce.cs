using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Numerics;
using Google.Protobuf;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using Phorkus.Proto;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionService : ITransactionService
    {
        private readonly Collection<string> _importedAddresses;
        private readonly RPCClient _rpcClient;
        private long _satoshiPerByte;

        public byte[] PublicKey { get; set; }

        internal BitcoinTransactionService()
        {
            _importedAddresses = new Collection<string>();
            var credentials = new NetworkCredential("user", "password");
            _rpcClient = new RPCClient(credentials, "127.0.0.1", Network.Main);
        }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripmd160;

        public ulong BlockGenerationTime { get; } = 10 * 60 * 1000;

        public ulong CurrentBlockHeight => (ulong) _rpcClient.GetBlockCount();

        public ulong TxConfirmation { get; } = 6;

        public IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight)
        {
            try
            {
                return _GetTransactionsAtBlockUnsafe(recipient, blockHeight);
            }
            catch (RPCException e)
            {
                throw new BlockchainNotAvailableException(e.Message);
            }
        }

        private IEnumerable<IContractTransaction> _GetTransactionsAtBlockUnsafe(byte[] recipient, ulong blockHeight)
        {
            // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses need to be cached.
            var getBlock = _rpcClient.GetBlock(blockHeight);
            var scriptPubKey =
                new BitcoinScriptAddress(Utils.ConvertByteArrayToString(recipient), NBitcoin.Network.Main)
                    .ScriptPubKey.PaymentScript;
            var bitcoinContractTransactions = new List<BitcoinContractTransaction>();
            foreach (var tx in getBlock.Transactions)
            {
                BigInteger totalValue = 0;
                var addTx = false;
                var from = FromRedeemToP2Sh(tx.Inputs.FirstOrDefault()?.ScriptSig.ToString());
                var badTx = false;
                foreach (var input in tx.Inputs)
                {
                    if (FromRedeemToP2Sh(input.ScriptSig.ToString()) != from)
                    {
                        badTx = true;
                        break;
                    }
                }

                if (badTx)
                    continue;

                foreach (var output in tx.Outputs)
                {
                    if (output.ScriptPubKey.IsUnspendable)
                    {
                        from = output.ScriptPubKey.ToString().Substring(4, BitcoinConfig.AddressLength);
                    }

                    var outputHex = output.ScriptPubKey.ToHex();
                    if (output.ScriptPubKey != scriptPubKey)
                    {
                        addTx = false;
                        break;
                    }

                    totalValue += output.Value.Satoshi;
                    addTx = true;
                }

                if (addTx)
                {
                    bitcoinContractTransactions.Add(new BitcoinContractTransaction(
                        Utils.ConvertHexStringToByteArray(from), totalValue, tx.GetHash().ToBytes(),
                        (ulong) getBlock.Header.BlockTime.ToUnixTimeSeconds()));
                }
            }

            return bitcoinContractTransactions;
        }

        public byte[] BroadcastTransaction(RawTransaction rawTransaction)
        {
            var sendRawTransaction = _rpcClient.SendRawTransaction(rawTransaction.TransactionData);
            if (sendRawTransaction is null)
                throw new BlockchainNotAvailableException("Unable to send transaction to Bitcoin network");
            return sendRawTransaction.ToBytes();
        }

        public bool IsTransactionConfirmed(byte[] txHash)
        {
            var getRawTransactionInfo =
                _rpcClient.GetRawTransactionInfo(uint256.Parse(Utils.ConvertByteArrayToString(txHash)));
            return getRawTransactionInfo != null && getRawTransactionInfo.Confirmations >= TxConfirmation;
        }

        public byte[] GenerateAddress(PublicKey publicKey)
        {
            return Utils.ConvertHexStringToByteArray("05" + Hashes.Hash160(Hashes
                                                         .Hash256(Utils.ConvertHexStringToByteArray(
                                                             "0014" + Hashes.Hash160(Hashes
                                                                 .Hash256(publicKey.ToByteArray()).ToBytes())))
                                                         .ToBytes()));
        }

        private static long _CalcBytes(int inputsNum, int outputsNum)
        {
            return inputsNum * BitcoinConfig.InputBytes + outputsNum * BitcoinConfig.OutputBytes +
                   BitcoinConfig.TxDataBytes;
        }

        internal long _GetFee(int inputsNum, int outputsNum, bool updateFee)
        {
            if (!updateFee)
                return _satoshiPerByte * _CalcBytes(inputsNum, outputsNum);
            var estimateFee = _rpcClient.EstimateSmartFee(1);
            _satoshiPerByte = (long) (estimateFee.FeeRate.SatoshiPerByte * (decimal) 1e8);
            return _satoshiPerByte * _CalcBytes(inputsNum, outputsNum);
        }

        internal KeyValuePair<List<OutPoint>, long> GetOutputs(string address, long value)
        {
            // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses need to be cached.
            if (!_importedAddresses.Contains(address))
            {
                _rpcClient.ImportAddress(new BitcoinScriptAddress(address, Network.Main));
                _importedAddresses.Add(address);
            }

            var listUnspent = _rpcClient.ListUnspent();
            long accumulateValue = 0;
            var outputs = new List<OutPoint>();
            _GetFee(0, 0, true);
            foreach (var output in listUnspent)
            {
                if (output.Address.ToString() != address)
                    continue;
                accumulateValue += output.Amount.Satoshi;
                outputs.Add(output.OutPoint);
                if (accumulateValue >= value + _GetFee(outputs.Count, 2, false))
                    break;
            }

            return new KeyValuePair<List<OutPoint>, long>(outputs, accumulateValue);
        }

        private static string FromRedeemToP2Sh(string redeemScript)
        {
            return Hashes.Hash160(Hashes.Hash256(Utils.ConvertHexStringToByteArray("0014" + redeemScript.Substring(6)))
                .ToBytes()).ToString();
        }
    }
}