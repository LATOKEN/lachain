using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
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
            _rpcClient = new RPCClient(NBitcoin.Network.Main);
        }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripmd160;

        public ulong BlockGenerationTime { get; } = 10 * 60 * 1000;

        public ulong CurrentBlockHeight => (ulong) _rpcClient.GetBlockCount();

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
            // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses needs to be cached.
            var getBlock = _rpcClient.GetBlock(blockHeight);
            var scriptPubKey =
                new BitcoinScriptAddress(Utils.ConvertByteArrayToString(recipient), NBitcoin.Network.Main)
                    .ScriptPubKey;
            var bitcoinContractTransactions = new List<BitcoinContractTransaction>();
            foreach (var tx in getBlock.Transactions)
            {
                BigInteger totalValue = 0;
                var addTx = false;
                var from = FromRedeemtoP2Sh(tx.Inputs.FirstOrDefault()?.ScriptSig.ToString());
                foreach (var output in tx.Outputs)
                {
                    if (output.ScriptPubKey != scriptPubKey)
                        continue;
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

        public byte[] BroadcastTransaction(ITransactionData transactionData)
        {
            var sendRawTransaction = _rpcClient.SendRawTransaction(transactionData.RawTransaction);
            if (sendRawTransaction is null)
                throw new BlockchainNotAvailableException("Unable to send transaction to Bitcoin network");
            return sendRawTransaction.ToBytes();
        }


        private static long _CalcBytes(int inputsNum, int outputsNum)
        {
            return inputsNum * BitcoinConfig.InputBytes + outputsNum * BitcoinConfig.OutputBytes +
                   BitcoinConfig.TxDataBytes;
        }

        internal long _GetFee(int inputsNum, int outputsNum, bool updateFee)
        {
            if (updateFee)
            {
                var estimateFee = _rpcClient.EstimateSmartFeeAsync(1);
                estimateFee.Wait();
                if (estimateFee.IsFaulted)
                {
                    throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                        "Bad request", RPCResponse.Load(Stream.Null));
                }

                _satoshiPerByte = (long) (estimateFee.Result.FeeRate.SatoshiPerByte * (decimal) 1e8);
            }

            return _satoshiPerByte * _CalcBytes(inputsNum, outputsNum);
        }

        internal KeyValuePair<List<OutPoint>, long> GetOutputs(string address, long value)
        {
            // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses needs to be cached.
            if (!_importedAddresses.Contains(address))
            {
                _rpcClient.ImportAddress(new BitcoinScriptAddress(address, NBitcoin.Network.Main));
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

        private static string FromRedeemtoP2Sh(string redeemScript)
        {
            return Hashes.Hash160(Hashes.Hash256(Utils.ConvertHexStringToByteArray("0014" + redeemScript.Substring(6)))
                .ToBytes()).ToString();
        }
    }
}