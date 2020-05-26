using System;
using System.Text;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class NativeTokenContract : ISystemContract
    {
        private readonly InvocationContext _context;

        public NativeTokenContract(InvocationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ContractStandard ContractStandard => ContractStandard.Lrc20;

        [ContractMethod(Lrc20Interface.MethodName)]
        public ExecutionStatus Name(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenNameCost);
            frame.ReturnValue = Encoding.ASCII.GetBytes("LaToken");
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
            frame.ReturnValue = Encoding.ASCII.GetBytes("LA");
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
            throw new NotImplementedException();
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public ExecutionStatus Approve(UInt160 spender, UInt256 value, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            throw new NotImplementedException();
        }

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public ExecutionStatus Allowance(UInt160 owner, UInt160 spender, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenAllowanceCost);
            throw new NotImplementedException();
        }
    }
}