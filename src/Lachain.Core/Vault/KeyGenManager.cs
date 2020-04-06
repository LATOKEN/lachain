using System;
using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Logger;

namespace Lachain.Core.Vault
{
    public class KeyGenManager
    {
        private static readonly ILogger<KeyGenManager> Logger =
            LoggerFactory.GetLoggerForClass<KeyGenManager>();

        public KeyGenManager(ITransactionManager transactionManager)
        {
            transactionManager.OnSystemContractInvoked += TransactionManagerOnOnSystemContractInvoked;
        }

        private static void TransactionManagerOnOnSystemContractInvoked(object sender, ContractContext context)
        {
            var tx = context.Receipt.Transaction;
            if (!tx.To.Equals(ContractRegisterer.GovernanceContract)) return;
            if (tx.Invocation.Length < 4) return;

            var signature = BitConverter.ToUInt32(tx.Invocation.Take(4).ToArray(), 0);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodChangeValidators))
            {
                var args = decoder.Decode(GovernanceInterface.MethodChangeValidators);
                var publicKeys =
                    (args[0] as byte[][] ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenCommit))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenSendValue))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenConfirm))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
            }
        }
    }
}