using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.VM;
using Lachain.Logger;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    public class PrivateKeysManager
    {
        private static readonly ILogger<PrivateKeysManager> Logger = LoggerFactory.GetLoggerForClass<PrivateKeysManager>();
        
        public PrivateKeysManager(ITransactionManager transactionManager)
        {
            transactionManager.OnSystemContractInvoked += TransactionManagerOnOnSystemContractInvoked;
        }

        private static void TransactionManagerOnOnSystemContractInvoked(object sender, ContractContext e)
        {
            if (!e.Transaction.To.Equals(ContractRegisterer.GovernanceContract)) return;
            var decoder = new ContractDecoder(e.Transaction.Invocation.ToArray());
            Logger.LogInformation(e.Transaction.Invocation.ToArray().ToHex());
            // e.Transaction.Invocation
        }
    }
}