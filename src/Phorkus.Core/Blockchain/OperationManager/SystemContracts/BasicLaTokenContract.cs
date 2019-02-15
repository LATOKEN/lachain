using System;
using Phorkus.Core.Blockchain.ContractManager;
using Phorkus.Core.Blockchain.ContractManager.Attributes;
using Phorkus.Core.Blockchain.ContractManager.Standards;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.SystemContracts
{
    public class BasicLaTokenContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        public BasicLaTokenContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }
        
        public ContractStandard ContractStandard => ContractStandard.Lrc20;

        [ContractProperty(Lrc20Interface.PropertyName)]
        public string Name()
        {
            return "LaToken";
        }

        [ContractProperty(Lrc20Interface.PropertyDecimals)]
        public uint Decimals()
        {
            return 18;
        }

        [ContractProperty(Lrc20Interface.PropertySymbol)]
        public string Symbol()
        {
            return "LA";
        }

        [ContractMethod(Lrc20Interface.MethodTotalSupply)]
        public void TotalSupply()
        {
            throw new NotImplementedException();
        }
        
        [ContractMethod(Lrc20Interface.MethodBalanceOf)]
        public UInt256 BalanceOf(UInt160 address)
        {
            var balance = _contractContext.SnapshotManager.CurrentSnapshot.Balances.GetBalance(address);
            return balance?.ToUInt256();
        }
        
        [ContractMethod(Lrc20Interface.MethodTransfer)]
        public bool Transfer(UInt160 recipient, UInt256 value)
        {
            _contractContext.SnapshotManager.CurrentSnapshot.Balances.TransferBalance(_contractContext.Sender, recipient, value.ToMoney());
            return true;
        }
        
        [ContractMethod(Lrc20Interface.MethodTransferFrom)]
        public void TransferFrom(UInt160 from, UInt160 recipient, UInt256 value)
        {
            throw new NotImplementedException();
        }
        
        [ContractMethod(Lrc20Interface.MethodApprove)]
        public void Approve(UInt160 spender, UInt256 value)
        {
            throw new NotImplementedException();
        }
        
        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public void Allowance(UInt160 owner, UInt160 spender)
        {
            throw new NotImplementedException();
        }
    }
}