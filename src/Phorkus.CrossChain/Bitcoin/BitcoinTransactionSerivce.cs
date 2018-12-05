using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using NBitcoin;
using NBitcoin.RPC;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionService : ITransactionService
    {
        public static KeyValuePair<List<OutPoint>, long> GetOutputs(string address, long value)
        {
            try
            {
                var rpcClient = new RPCClient(NBitcoin.Network.Main);
                // ImportAddress is a heavy rpc call that rescans whole blockchain, addresses needs to be cached.
                rpcClient.ImportAddress(new BitcoinScriptAddress(address, NBitcoin.Network.Main));
                var listUnspent = rpcClient.ListUnspent();
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

        public bool CommitTransactionsBatch(ITransactionData[] transactionData)
        {
            return true;
        }


        public bool StoreTransaction(ITransactionData transactionData)
        {
            return true;
        }

        public bool CommitTransaction(ITransactionData transactionData)
        {
            return true;
        }
    }
}