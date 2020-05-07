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
using NetMQ;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class ReserveFundTokenContract : ISystemContract
    {
        private readonly ContractContext _contractContext;
        
        private readonly StorageVariable _totalSupply;
        private readonly StorageMapping _allowance;
        private readonly StorageMapping _balance;

        public ReserveFundTokenContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _balance = new StorageMapping(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
            _totalSupply = new StorageVariable(
                ContractRegisterer.LatokenContract,
                contractContext.Snapshot.Storage,
                BigInteger.One.ToUInt256()
            );
            _allowance = new StorageMapping(
                ContractRegisterer.GovernanceContract,
                contractContext.Snapshot.Storage,
                new BigInteger(2).ToUInt256()
            );
        }
        
        public ContractStandard ContractStandard => ContractStandard.Lrc20;

        [ContractProperty(Lrc20Interface.PropertyName)]
        public string Name()
        {
            return "LaReserveFundToken";
        }

        [ContractProperty(Lrc20Interface.PropertyDecimals)]
        public uint Decimals()
        {
            return 18;
        }

        [ContractProperty(Lrc20Interface.PropertySymbol)]
        public string Symbol()
        {
            return "LRF";
        }

        [ContractMethod(Lrc20Interface.MethodTotalSupply)]
        public UInt256 TotalSupply()
        {
            var totalSupply = _totalSupply.Get();
            return totalSupply.Length == 0 ? UInt256Utils.Zero : totalSupply.ToUInt256();
        }

        [ContractMethod(Lrc20Interface.MethodBalanceOf)]
        public UInt256 BalanceOf(UInt160 address)
        {
            /* TODO: add gas metering */
            var balance = _balance.GetValue(address.ToBytes());
            return balance.Length == 0 ? UInt256Utils.Zero : balance.ToUInt256();
        }

        [ContractMethod(Lrc20Interface.MethodTransfer)]
        public bool Transfer(UInt160 recipient, UInt256 value)
        {
            var availableBalance = BalanceOf(MsgSender());
            if (availableBalance.ToMoney(true).CompareTo(value.ToMoney(true)) < 0)
                return false;
            InternalTransfer(MsgSender(), recipient, value);
            return true;
        }

        public bool Mint(UInt160 to, UInt256 amount)
        {
            if (!MsgSender().Equals(ContractRegisterer.ReserveFundContract)) return false;
            AddSupply(amount);
            AddBalance(to, amount);
            return true;
        }

        public bool Burn(UInt160 from, UInt256 amount)
        {
            if (!MsgSender().Equals(ContractRegisterer.ReserveFundContract)) return false;
            var availableBalance = BalanceOf(from);
            if (availableBalance.ToMoney(true).CompareTo(amount.ToMoney(true)) < 0)
                return false;
            SubSupply(amount);
            SubBalance(from, amount);
            return true;
        }

        [ContractMethod(Lrc20Interface.MethodTransferFrom)]
        public bool TransferFrom(UInt160 from, UInt160 to, UInt256 value)
        {
            var availableAllowance = Allowance(from, MsgSender());
            if (availableAllowance.ToMoney(true).CompareTo(value.ToMoney(true)) < 0)
                return false;
            
            var availableBalance = BalanceOf(from);
            if (availableBalance.ToMoney(true).CompareTo(value.ToMoney(true)) < 0)
                return false;
            
            SubAllowance(from, MsgSender(), value);
            InternalTransfer(from, to, value);
            return true;
        }

        private void InternalTransfer(UInt160 from, UInt160 to, UInt256 value)
        {
            SubBalance(from, value);
            AddBalance(to, value);
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public bool Approve(UInt160 spender, UInt256 amount)
        {
            SetAllowance(MsgSender(), spender, amount);
            return true;
        }

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public UInt256 Allowance(UInt160 owner, UInt160 spender)
        {
            var allowance = _allowance.GetValue(owner.ToBytes().Concat(spender.ToBytes()));
            return allowance.Length == 0 ? UInt256Utils.Zero : allowance.ToUInt256();
        }

        private void SubAllowance(UInt160 owner, UInt160 spender, UInt256 value)
        {
            var allowance = Allowance(owner, spender).ToMoney(true);
            allowance -= value.ToMoney(true);
            SetAllowance(owner, spender, allowance.ToUInt256());
        }
        
        private void SetAllowance(UInt160 owner, UInt160 spender, UInt256 amount)
        {
            _allowance.SetValue(owner.ToBytes().Concat(spender.ToBytes()), amount.ToBytes());
        }

        private UInt160 MsgSender()
        {
            return _contractContext.Sender ?? throw new InvalidOperationException();
        }

        private void SubBalance(UInt160 owner, UInt256 value)
        {
            var balance = BalanceOf(owner).ToMoney(true);
            balance -= value.ToMoney(true);
            SetBalance(owner, balance.ToUInt256());
        }

        private void AddBalance(UInt160 owner, UInt256 value)
        {
            var balance = BalanceOf(owner).ToMoney(true);
            balance += value.ToMoney(true);
            SetBalance(owner, balance.ToUInt256());
        }

        private void AddSupply(UInt256 value)
        {
            var totalSupply = TotalSupply().ToMoney(true);
            totalSupply += value.ToMoney(true);
            SetTotalSupply(totalSupply.ToUInt256());
        }

        private void SubSupply(UInt256 value)
        {
            var totalSupply = TotalSupply().ToMoney(true);
            totalSupply -= value.ToMoney(true);
            SetTotalSupply(totalSupply.ToUInt256());
        }
        
        private void SetBalance(UInt160 owner, UInt256 amount)
        {
            _balance.SetValue(owner.ToBytes(), amount.ToBytes());
        }
        
        private void SetTotalSupply(UInt256 value)
        {
            _totalSupply.Set(value.ToBytes());
        }
    }
}