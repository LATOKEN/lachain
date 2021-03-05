using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class NativeTokenContract : ISystemContract
    {
        private static readonly ILogger<NativeTokenContract> Logger =
            LoggerFactory.GetLoggerForClass<NativeTokenContract>();

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
            var supply = _context.Snapshot?.Balances.GetSupply();
            if (supply is null) return ExecutionStatus.ExecutionHalted;
            frame.ReturnValue = supply.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
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

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public ExecutionStatus Allowance(UInt160 owner, UInt160 spender, SystemContractExecutionFrame frame)
        {
            var allowance = GetAllowance(owner, spender);
            frame.ReturnValue = allowance.ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodTransfer)]
        public ExecutionStatus Transfer(UInt160 recipient, UInt256 value, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenTransferCost);
            var from = _context.Sender ?? throw new InvalidOperationException();
            var result = _context.Snapshot.Balances.TransferBalance(
                from,
                recipient,
                value.ToMoney()
            );
            Emit(Lrc20Interface.EventTransfer, from, recipient, value);
            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodTransferFrom)]
        public ExecutionStatus TransferFrom(
            UInt160 from, UInt160 recipient, UInt256 value, SystemContractExecutionFrame frame
        )
        {
            frame.UseGas(GasMetering.NativeTokenTransferFromCost);
            if (!SubAllowance(from, Sender(), value, frame))
                return ExecutionStatus.ExecutionHalted;
            var result = _context.Snapshot.Balances.TransferBalance(from, recipient, value.ToMoney());
            Emit(Lrc20Interface.EventTransfer, from, recipient, value);
            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public ExecutionStatus Approve(UInt160 spender, UInt256 amount, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            SetAllowance(Sender(), spender, amount);
            Emit(Lrc20Interface.EventApproval, Sender(), spender, amount);
            frame.ReturnValue = 1.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        private bool SubAllowance(UInt160 owner, UInt160 spender, UInt256 value, SystemContractExecutionFrame frame)
        {
            var allowance = GetAllowance(owner, spender).ToMoney();
            if (allowance < value.ToMoney()) return false;
            allowance -= value.ToMoney();
            SetAllowance(owner, spender, allowance.ToUInt256());
            return true;
        }

        private UInt256 GetAllowance(UInt160 owner, UInt160 spender)
        {
            var amountBytes = _allowance.GetValue(owner.ToBytes().Concat(spender.ToBytes()));
            return amountBytes.Length == 0 ? UInt256Utils.Zero : amountBytes.ToUInt256();
        }

        private void SetAllowance(UInt160 owner, UInt160 spender, UInt256 amount)
        {
            _allowance.SetValue(owner.ToBytes().Concat(spender.ToBytes()), amount.ToBytes());
        }

        private UInt160 Sender()
        {
            return _context.Sender ?? throw new InvalidOperationException();
        }

        private static string PrettyParam(dynamic param)
        {
            return param switch
            {
                UInt256 x => x.ToBytes().Reverse().ToHex(),
                UInt160 x => x.ToBytes().ToHex(),
                byte[] b => b.ToHex(),
                byte[][] s => string.Join(", ", s.Select(t => t.ToHex())),
                _ => param.ToString()
            };
        }

        private void Emit(string eventSignature, params dynamic[] values)
        {
            var eventData = ContractEncoder.Encode(eventSignature, values);
            var eventObj = new Event
            {
                Contract = ContractRegisterer.LatokenContract,
                Data = ByteString.CopyFrom(eventData),
                TransactionHash = _context.Receipt.Hash
            };
            _context.Snapshot.Events.AddEvent(eventObj);
            Logger.LogDebug($"Event: {eventSignature}, params: {string.Join(", ", values.Select(PrettyParam))}");
            Logger.LogTrace($"Event data ABI encoded: {eventData.ToHex()}");
        }
    }
}