using System;
using System.Linq;
using System.Numerics;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class NativeTokenContract : ISystemContract
    {
        private readonly InvocationContext _context;

        private readonly StorageMapping _allowance;

        public NativeTokenContract(InvocationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _allowance = new StorageMapping(
                ContractRegisterer.LatokenContract,
                context.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
        }

        public ContractStandard ContractStandard => ContractStandard.Lrc20;

        [ContractMethod(Lrc20Interface.MethodName)]
        public ExecutionStatus Name(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenNameCost);
            frame.ReturnValue = "LaToken".EncodeString();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodDecimals)]
        public ExecutionStatus Decimals(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenDecimalsCost);
            frame.ReturnValue = 18.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodSymbol)]
        public ExecutionStatus Symbol(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenSymbolCost);
            frame.ReturnValue = "LA".EncodeString();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodTotalSupply)]
        public ExecutionStatus TotalSupply(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenTotalSupplyCost);
            throw new NotImplementedException();
        }

        [ContractMethod(Lrc20Interface.MethodBalanceOf)]
        public ExecutionStatus BalanceOf(UInt160 address, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenBalanceOfCost);
            var balance = _context.Snapshot?.Balances.GetBalance(address);
            if (balance is null) return ExecutionStatus.ExecutionHalted;
            frame.ReturnValue = balance.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodTransfer)]
        public ExecutionStatus Transfer(UInt160 recipient, UInt256 value, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenTransferCost);
            if (_context.Snapshot is null) return ExecutionStatus.ExecutionHalted;
            var result = _context.Snapshot.Balances.TransferBalance(
                _context.Sender ?? throw new InvalidOperationException(),
                recipient, value.ToMoney()
            );
            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodTransferFrom)]
        public ExecutionStatus TransferFrom(UInt160 from, UInt160 recipient, UInt256 value, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenTransferFromCost);
            SubAllowance(from, MsgSender(), value, frame);
            var result = _context.Snapshot.Balances.TransferBalance(from,recipient, value.ToMoney());
            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public ExecutionStatus Approve(UInt160 spender, UInt256 amount, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            if (_context.Snapshot is null) return ExecutionStatus.ExecutionHalted;
            SetAllowance(MsgSender(), spender, amount);
            frame.ReturnValue = 1.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public ExecutionStatus Allowance(UInt160 owner, UInt160 spender, SystemContractExecutionFrame frame)
        {
            var amountBytes = _allowance.GetValue(owner.ToBytes().Concat(spender.ToBytes()));
            if (amountBytes.Length == 0) frame.ReturnValue = UInt256Utils.Zero.ToBytes();
            frame.ReturnValue = amountBytes.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }
        
        private void SetAllowance(UInt160 owner, UInt160 spender, UInt256 amount)
        {
            _allowance.SetValue(owner.ToBytes().Concat(spender.ToBytes()), amount.ToBytes());
        }

        private UInt160 MsgSender()
        {
            return _context.Sender ?? throw new InvalidOperationException();
        }

        public void SubAllowance(UInt160 owner, UInt160 spender, UInt256 value, SystemContractExecutionFrame frame)
        {
            Allowance(owner, spender, frame);
            var allowance = frame.ReturnValue.ToUInt256().ToMoney();
            allowance -= value.ToMoney();
            SetAllowance(owner, spender, allowance.ToUInt256());
        }
    }
}