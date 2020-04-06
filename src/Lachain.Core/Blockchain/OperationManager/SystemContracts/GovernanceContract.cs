using System;
using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Attributes;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.OperationManager.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        public GovernanceContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }
        
        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        public void ChangeValidators(byte[][] newValidators)
        {
        }

        [ContractProperty(GovernanceInterface.MethodKeygenCommit)]
        public void KeyGenCommit(byte[] commitment, byte[] encryptedRows)
        {
            
        }

        [ContractProperty(GovernanceInterface.MethodKeygenSendValue)]
        public void KeyGenSendValue(byte[] encryptedValue)
        {
            
        }
        
        [ContractProperty(GovernanceInterface.MethodKeygenConfirm)]
        public void KeyGenConfirm(UInt256 hash)
        {
            
        }
        
        
    }
}