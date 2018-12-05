using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionService : ITransactionService
    {
        private RPCClient _rpcClient;
        
        public byte[] PublicKey { get; set; }

        internal BitcoinTransactionService()
        {
            _rpcClient = new RPCClient(NBitcoin.Network.Main);
        }
        
        public BigInteger GetLastBlockHeight()
        {
            return _rpcClient.GetBlockCount();
        }

        
        public static long calcBytes(int inputsNum, int outputsNum)
        {
            return inputsNum * BitcoinConfig.InputBytes + outputsNum * BitcoinConfig.OutputBytes +
                   BitcoinConfig.TxDataBytes;
        }
        
        public long GetFee(int inputsNum, int outputsNum)
        {
            var rpcClient = new RPCClient(NBitcoin.Network.Main);
            var estimateFee = rpcClient.EstimateSmartFeeAsync(1);
            estimateFee.Wait();
            if (estimateFee.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            var satoshiPerByte = (long) (estimateFee.Result.FeeRate.SatoshiPerByte * (decimal) 1e8);
            return satoshiPerByte * calcBytes(inputsNum, outputsNum);
        }


        public KeyValuePair<List<OutPoint>, long> GetOutputs(string address, long value)
        {
            try
            {
                // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses needs to be cached.
                _rpcClient.ImportAddress(new BitcoinScriptAddress(address, NBitcoin.Network.Main));
                var listUnspent = _rpcClient.ListUnspent();
                long accumulateValue = 0;
                var outputs = new List<OutPoint>();
                foreach (var output in listUnspent)
                {
                    if (output.Address.ToString() == address)
                    {
                        accumulateValue += output.Amount.Satoshi;
                        outputs.Add(output.OutPoint);
                        if (accumulateValue >= value)
                        {
                            break;
                        }
                    }
                }

                return new KeyValuePair<List<OutPoint>, long>(outputs, accumulateValue);
            }
            catch (RPCException)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }
        }

        public static string fromRedeemtoP2SH(string redeemScript)
        {
            return Hashes
                .Hash160(Hashes
                    .Hash256(Utils.ConvertHexStringToByteArray("0014" + redeemScript.Substring(6)))
                    .ToBytes()).ToString();
        }

        public ulong CurrentBlockHeight { get; }
        
        public IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight)
        {
            try
            {
                var rpcClient = new RPCClient(NBitcoin.Network.Main);
                // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses needs to be cached.
                var getTransactions = rpcClient.GetTransactions(blockHeight);
                var scriptPubKey =
                    new BitcoinScriptAddress(Utils.ConvertByteArrayToString(recipient), NBitcoin.Network.Main)
                        .ScriptPubKey;
                var bitcoinContractTransactions = new List<BitcoinContractTransaction>();
                foreach (var tx in getTransactions)
                {
                    BigInteger totalValue = 0;
                    var addTx = false;
                    var from = fromRedeemtoP2SH(tx.Inputs.FirstOrDefault().ScriptSig.ToString());
                    foreach (var output in tx.Outputs)
                    {
                        if (output.ScriptPubKey == scriptPubKey)
                        {
                            totalValue += output.Value.Satoshi;
                            addTx = true;
                        }
                    }

                    if (addTx)
                    {
                        bitcoinContractTransactions.Add(new BitcoinContractTransaction(BlockchainType.Bitcoin,
                            Utils.ConvertHexStringToByteArray(from), AddressFormat.Ripmd160, totalValue));
                    }
                }

                return bitcoinContractTransactions;
            }

            catch (RPCException)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }
        }

        public bool BroadcastTransactionsBatch(ITransactionData[] transactionData)
        {
            return true;
        }


        public bool StoreTransaction(ITransactionData transactionData)
        {
            return true;
        }

        public bool BroadcastTransaction(ITransactionData transactionData)
        {
            var sendRawTransaction = _rpcClient.SendRawTransaction(transactionData.RawTransaction);
            return true;
        }
    }
}