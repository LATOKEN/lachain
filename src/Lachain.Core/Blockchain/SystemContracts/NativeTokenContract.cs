using System;
using System.Linq;
using System.Numerics;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class NativeTokenContract : ISystemContract
    {
        private readonly ContractContext _contractContext;
        
        private readonly StorageMapping _allowance;

        public NativeTokenContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _allowance = new StorageMapping(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
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
        public UInt256? BalanceOf(UInt160 address)
        {
            /* TODO: add gas metering */
            var balance = _contractContext.Snapshot?.Balances.GetBalance(address);
            return balance?.ToUInt256();
        }

        [ContractMethod(Lrc20Interface.MethodTransfer)]
        public bool Transfer(UInt160 recipient, UInt256 value)
        {
            if (_contractContext.Snapshot is null) return false;
            _contractContext.Snapshot.Balances.TransferBalance(
                MsgSender(),
                recipient, value.ToMoney(true)
            );
            return true;
        }

        [ContractMethod(Lrc20Interface.MethodTransferFrom)]
        public bool TransferFrom(UInt160 from, UInt160 recipient, UInt256 value)
        {
            if (_contractContext.Snapshot is null) return false;
            SubAllowance(from, MsgSender(), value);
            _contractContext.Snapshot.Balances.TransferBalance(from,recipient, value.ToMoney(true));
            return true;
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public bool Approve(UInt160 spender, UInt256 amount)
        {
            if (_contractContext.Snapshot is null) return false;
            SetAllowance(MsgSender(), spender, amount);
            return true;
        }

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public UInt256 Allowance(UInt160 owner, UInt160 spender)
        {
            var amountBytes = _allowance.GetValue(owner.ToBytes().Concat(spender.ToBytes()));
            if (amountBytes.Length == 0) return UInt256Utils.Zero;
            return amountBytes.ToUInt256();
        }
        
        private void SetAllowance(UInt160 owner, UInt160 spender, UInt256 amount)
        {
            _allowance.SetValue(owner.ToBytes().Concat(spender.ToBytes()), amount.ToBytes());
        }

        private UInt160 MsgSender()
        {
            return _contractContext.Sender ?? throw new InvalidOperationException();
        }

        public UInt256 SubAllowance(UInt160 owner, UInt160 spender, UInt256 value)
        {
            var allowance = Allowance(owner, spender).ToMoney(true);
            allowance -= value.ToMoney(true);
            SetAllowance(owner, spender, allowance.ToUInt256());
            return allowance.ToUInt256();
        }
    }
}