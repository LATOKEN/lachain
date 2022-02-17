using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.Protobuf;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class NativeTokenContract : ISystemContract
    {
        private static readonly ILogger<NativeTokenContract> Logger =
            LoggerFactory.GetLoggerForClass<NativeTokenContract>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly InvocationContext _context;

        private readonly StorageMapping _allowance;

        // private static EcdsaKeyPair _minterKeyPair =
        //     new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes()
        //         .ToPrivateKey());
        // private static byte[] _minterPubKey = CryptoUtils.EncodeCompressed(_minterKeyPair.PublicKey);
        // private static UInt160 _minterAdd = Crypto.ComputeAddress(_minterPubKey).ToUInt160();

        private static EcdsaKeyPair _mintCntrlKeyPair =
            new EcdsaKeyPair("0xE83385AF76B2B1997326B567461FB73DD9C27EAB9E1E86D26779F4650C5F2B75".HexToBytes()
                .ToPrivateKey());
        private static byte[] _mintCntrlPubKey = CryptoUtils.EncodeCompressed(_mintCntrlKeyPair.PublicKey);
        private static UInt160 _mintCntrlAdd = Crypto.ComputeAddress(_mintCntrlPubKey).ToUInt160();

        private readonly Money _maxSupply = Money.Parse("1000000000");

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
            frame.ReturnValue = ContractEncoder.Encode(null, 18.ToUInt256());
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
            if (supply is null) 
                return ExecutionStatus.ExecutionHalted;
            frame.ReturnValue = ContractEncoder.Encode(null, supply);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodBalanceOf)]
        public ExecutionStatus BalanceOf(UInt160 address, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenBalanceOfCost);
            var balance = _context.Snapshot?.Balances.GetBalance(address);
            if (balance is null) 
                return ExecutionStatus.ExecutionHalted;
            frame.ReturnValue = ContractEncoder.Encode(null, balance);
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodAllowance)]
        public ExecutionStatus Allowance(UInt160 owner, UInt160 spender, SystemContractExecutionFrame frame)
        {
            var allowance = GetAllowance(owner, spender);
            frame.ReturnValue = ContractEncoder.Encode(null, allowance);
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
            frame.ReturnValue = ContractEncoder.Encode(null, (result ? 1 : 0).ToUInt256());
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
            frame.ReturnValue = ContractEncoder.Encode(null, (result ? 1 : 0).ToUInt256());
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodApprove)]
        public ExecutionStatus Approve(UInt160 spender, UInt256 amount, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            SetAllowance(Sender(), spender, amount);
            Emit(Lrc20Interface.EventApproval, Sender(), spender, amount);
            frame.ReturnValue = ContractEncoder.Encode(null, 1.ToUInt256());
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodSetAllowedSupply)]
        public ExecutionStatus SetAllowedSupply(UInt256 amount, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            if (!frame.InvocationContext.Sender.Equals(_mintCntrlAdd))
                return ExecutionStatus.ExecutionHalted;

            var amountMoney = amount.ToMoney();
            if (amountMoney > _maxSupply)
                return ExecutionStatus.ExecutionHalted;

            if (amountMoney <= _context.Snapshot.Balances.GetSupply())
                return ExecutionStatus.ExecutionHalted;

            _context.Snapshot.Balances.SetAllowedSupply(amountMoney);

            frame.ReturnValue = ContractEncoder.Encode(null, _context.Snapshot.Balances.GetAllowedSupply());
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodGetAllowedSupply)]
        public ExecutionStatus GetAllowedSupply(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            frame.ReturnValue = ContractEncoder.Encode(null, _context.Snapshot.Balances.GetAllowedSupply());
            return ExecutionStatus.Ok;
        }

        [ContractMethod(Lrc20Interface.MethodMint)]
        public ExecutionStatus Mint(UInt160 address, UInt256 amount, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            if (!frame.InvocationContext.Sender.Equals(_context.Snapshot.Balances.GetMinter()))
                return ExecutionStatus.ExecutionHalted;

            var totalSupply = _context.Snapshot.Balances.GetSupply();

            var amountMoney = amount.ToMoney();
            if (totalSupply + amountMoney > _maxSupply ||
                totalSupply + amountMoney > _context.Snapshot.Balances.GetAllowedSupply())
                return ExecutionStatus.ExecutionHalted;

            var newBalance = _context.Snapshot?.Balances.AddBalance(address, amountMoney,  true);
            if (newBalance is null) 
                return ExecutionStatus.ExecutionHalted;
            Emit(Lrc20Interface.EventMinted, address, amount);
            frame.ReturnValue = ContractEncoder.Encode(null, newBalance);
            return ExecutionStatus.Ok;
        }
        
        [ContractMethod(Lrc20Interface.MethodSetMinter)]
        public ExecutionStatus SetMinter(UInt160 minterAddress, SystemContractExecutionFrame frame)
        {
            Logger.LogInformation($"SetMinter, Sender {frame.InvocationContext.Sender.ToHex()}, minterController {_mintCntrlAdd.ToHex()}.  minter {minterAddress.ToHex()}");
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            if (!frame.InvocationContext.Sender.Equals(_mintCntrlAdd))
            {
                return ExecutionStatus.ExecutionHalted;
            }

            _context.Snapshot.Balances.SetMinter(minterAddress);
            frame.ReturnValue = ContractEncoder.Encode(null, _context.Snapshot.Balances.GetMinter().ToUInt256());
            return ExecutionStatus.Ok;
        }
        
        [ContractMethod(Lrc20Interface.MethodGetMinter)]
        public ExecutionStatus GetMinter(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.NativeTokenApproveCost);
            frame.ReturnValue = ContractEncoder.Encode(null, _context.Snapshot.Balances.GetMinter().ToUInt256());
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
                UInt256 x => x.ToBytes().ToHex(),
                UInt160 x => x.ToBytes().ToHex(),
                byte[] b => b.ToHex(),
                byte[][] s => string.Join(", ", s.Select(t => t.ToHex())),
                _ => param.ToString()
            };
        }

        private void Emit(string eventSignature, params dynamic[] values)
        {
            var eventData = ContractEncoder.Encode(null, values);
            var eventObj = new EventObject(
                new Event
                {
                    Contract = ContractRegisterer.LatokenContract,
                    Data = ByteString.CopyFrom(eventData),
                    TransactionHash = _context.Receipt.Hash,
                    SignatureHash = ContractEncoder.MethodSignature(eventSignature).ToArray().ToUInt256()
                }
            );
            _context.Snapshot.Events.AddEvent(eventObj);
            Logger.LogDebug($"Event: {eventSignature}, sighash: {eventObj._event.SignatureHash.ToHex()}, params: {string.Join(", ", values.Select(PrettyParam))}");
            Logger.LogTrace($"Event data ABI encoded: {eventData.ToHex()}");
        }
    }
}