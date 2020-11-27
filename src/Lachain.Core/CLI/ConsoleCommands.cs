using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;
using System.Linq;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.ValidatorStatus;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.CLI
{
    public class ConsoleCommands : IConsoleCommands
    {
        private const uint TxLength = 32;

        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ISystemContractReader _systemContractReader;
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly EcdsaKeyPair _keyPair;

        public ConsoleCommands(
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            ITransactionSigner transactionSigner,
            IBlockManager blockManager,
            IStateManager stateManager,
            ISystemContractReader systemContractReader,
            IValidatorStatusManager validatorStatusManager,
            EcdsaKeyPair keyPair
        )
        {
            _blockManager = blockManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _transactionSigner = transactionSigner;
            _stateManager = stateManager;
            _systemContractReader = systemContractReader;
            _validatorStatusManager = validatorStatusManager;
            _keyPair = keyPair;
        }

        private static bool IsValidHexString(IEnumerable<char> hexString)
        {
            return hexString.Select(currentCharacter =>
                currentCharacter >= '0' && currentCharacter <= '9' ||
                currentCharacter >= 'a' && currentCharacter <= 'f' ||
                currentCharacter >= 'A' && currentCharacter <= 'F').All(isHexCharacter => isHexCharacter);
        }

        private static string EraseHexPrefix(string hexString)
        {
            if (hexString.StartsWith("0x"))
                hexString = hexString.Substring(2);
            return hexString;
        }

        /*
         * GetTransaction
         * blockHash, UInt256
        */
        public string? GetTransaction(string[] arguments)
        {
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
            {
                return null;
            }

            var tx = _transactionManager.GetByHash(arguments[1].HexToUInt256()) ??
                     throw new InvalidOperationException();
            return ProtoUtils.ParsedObject(tx);
            /*
            var type = tx.Transaction.Type;
            var txType = tx.Transaction.GetType().GetField(type.ToString());
            var txByType = txType.GetValue(tx.Transaction);
            var value = (UInt256) txByType.GetType().GetField("Value").GetValue(txByType);
            var to = (UInt160) txByType.GetType().GetField("To").GetValue(txByType);
            if (type != TransactionType.Contract)
            {
                to = (UInt160) txByType.GetType().GetField("Recipient").GetValue(txByType);
            }
            if (type == Tra)
            var assetName = (Asset) txByType.GetType().GetField("Asset").GetValue(txByType);
            var 
            return
                $"Hash: {tx.Hash.ToHex()}\n" +
                $"Signature: {tx.Signature.ToByteArray()}\n" +
                $"Type: {tx.Transaction.Type}\n" +
                $"Nonce: {tx.Transaction.Nonce}\n" + value == null ? $"Value: {value.ToString()} : "";*/
        }

        /*
         * GetBlock
         * blockHash, UInt256
        */
        public string? GetBlock(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            var value = EraseHexPrefix(arguments[1]);
            return ulong.TryParse(value, out var blockHeight)
                ? ProtoUtils.ParsedObject(_blockManager.GetByHeight(blockHeight) ??
                                          throw new InvalidOperationException())
                : ProtoUtils.ParsedObject(_blockManager.GetByHash(arguments[1].HexToUInt256()) ??
                                          throw new InvalidOperationException());
        }

        /*
         * GetBalance
         * address, UInt160
         * asset, UInt160
        */
        public Money? GetBalance(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            return _stateManager.LastApprovedSnapshot.Balances
                .GetBalance(arguments[1].HexToUInt160());
        }

        public string DeployContract(string[] arguments)
        {
            var from = _keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var hash = from.ToBytes().Concat(nonce.ToBytes()).Keccak();
            var byteCode = arguments[1].HexToBytes();
            if (!VirtualMachine.VerifyContract(byteCode))
                return "Unable to validate smart-contract code";
            Console.WriteLine("Contract Hash: " + hash.ToHex());
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = _transactionSigner.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.ToHex();
        }

        public string CallContract(string[] arguments)
        {
            return "";
            // var from = _keyPair.PublicKey.GetAddress();
            // var contractHash = arguments[1].HexToUInt160();
            // var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            // if (contract is null)
            //     return $"Unable to find contract by hash {contractHash.ToHex()}";
            // Console.WriteLine("Code: " + contract.ByteCode.ToByteArray().ToHex());
            // var result = _stateManager.SafeContext(() =>
            // {
            //     _stateManager.NewSnapshot();
            //     var invocationResult =
            //         _virtualMachine.InvokeWasmContract(contract, new InvocationContext(from), new byte[] { }, 200_000);
            //     _stateManager.Rollback();
            //     return invocationResult;
            // });
            // return result.Status == ExecutionStatus.Ok
            //     ? "Contract has been successfully executed"
            //     : "Contract execution failed";
        }

        public string Help(string[] arguments)
        {
            var methods = GetType().GetMethods().Select(m =>
            {
                var args = m.GetParameters().Select(arg => $"{arg.ParameterType.Name} {arg.Name}");
                return $" - {m.Name.ToLower()}: {string.Join(", ", args)}";
            });
            return "Commands:\n" + string.Join("\n", methods);
        }

        /*
         * GetTransactionPool
        */
        public IEnumerable<string> GetTransactionPool(string[] arguments)
        {
            return _transactionPool.Transactions.Values.Select(transaction => transaction.Hash.ToHex()).ToList();
        }

        /*
         * SignBlock
         * blockHash, UInt256
        */
        public string? SignBlock(string[] arguments)
        {
            if (arguments.Length != 2 || arguments[1].Length != TxLength)
                return null;

            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var block = _blockManager.GetByHash(arguments[1].HexToUInt256());
            return block is null ? null : _blockManager.Sign(block.Header, _keyPair).ToByteArray().ToString();
        }

        public string SignTransaction(string[] arguments)
        {
            var to = arguments[1].HexToUInt160();
            var value = Money.Parse(arguments[2]);
            var from = _keyPair.PublicKey.GetAddress();
            var tx = _transactionBuilder.TransferTransaction(from, to, value);
            var signedTx = _transactionSigner.Sign(tx, _keyPair);
            return signedTx.Signature.ToString();
        }

        public string? SendRawTransaction(string[] arguments)
        {
            if (arguments.Length != 3)
                return null;
            var rawTx = arguments[1].HexToBytes();
            var tx = Transaction.Parser.ParseFrom(rawTx);
            var sig = arguments[2].HexToBytes().ToSignature();
            var result = _transactionPool.Add(tx, sig);
            Console.WriteLine($"Status: {result}");
            Console.WriteLine($"Hash: {tx.FullHash(sig).ToHex()}");
            return "";
        }

        /*
         * SendTransaction:
         * 1. to, UInt160,
         * 2. assetName, string
         * 3. value, UInt256,
         * 4. fee, UInt256
        */
        public string SendTransaction(string[] arguments)
        {
            var to = arguments[1].HexToUInt160();
            var value = Money.Parse(arguments[2]);
            var fee = Money.Parse(arguments[3]);
            var from = _keyPair.PublicKey.GetAddress();
            var tx = _transactionBuilder.TransferTransaction(from, to, value);
            var signedTx = _transactionSigner.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.ToHex();
        }

        /// <summary>
        /// Verify Transaction:
        ///  1. raw transaction in hex
        ///  2. raw signature in hex
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public string VerifyTransaction(string[] arguments)
        {
            arguments[1] =
                "0x080322160a14e855e8f8e5f66a84c62800e9fc8fa06d77c35baf323b0a160a14309ef5b9fed49a18eb3ea1d090c79df690936b9812160a146bc32575acb8754886dc283c2c8ac54b1bd931951a090a072386f26fc100007200";
            arguments[2] =
                "0x01aa279be6f82767f7d1c75a966b33c13d2ae573f7f39ccf7557d86cc0cdb8aa5731b2639ff6ef7555232fd1ed6e27e281e5ae96de22b49083df380fb892485761";

            var tx = Transaction.Parser.ParseFrom(
                arguments[1].HexToBytes());
            var sig = arguments[2].HexToBytes().ToSignature();
            Console.WriteLine($"Tx Hash: {tx.FullHash(sig)}");
            var accepted = new TransactionReceipt
            {
                Transaction = tx,
                Hash = tx.FullHash(sig),
                Signature = sig
            };
            Console.WriteLine("Transaction validity: " + _transactionManager.Verify(accepted));
            Console.WriteLine(_transactionManager.VerifySignature(accepted) == OperatingError.Ok
                ? "Signature validity: OK"
                : "Signature validity: INVALID");
            Console.WriteLine(_transactionManager.VerifySignature(accepted, false) == OperatingError.Ok
                ? "Signature validity: OK"
                : "Signature validity: INVALID");
            return "\n";
        }

        /// <summary>
        /// CurrentStake:
        ///  outputs current stake size
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>current stake size</returns>
        public string CurrentStake(string[] arguments)
        {
            var stake = _systemContractReader.GetStake().ToMoney();
            return $"{stake}";
        }

        /// <summary>
        /// Stake:
        ///  stake amount
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>stake tx hash/returns>
        public string NewStake(string[] arguments)
        {
            if (_validatorStatusManager.IsStarted())
                return "ERROR: Withdraw current stake first";
            if (arguments.Length == 0)
                _validatorStatusManager.Start(false);
            else
                _validatorStatusManager.StartWithStake(Money.Parse(arguments[0]).ToUInt256(false));
            return "Validator is started";
        }

        /// <summary>
        /// ValidatorStatus:
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>tx hash</returns>
        public string ValidatorStatus(string[] arguments)
        {
            if (!_validatorStatusManager.IsStarted())
                return "Validator is off";
            if (_validatorStatusManager.IsWithdrawTriggered())
                return "Stake withdraw is triggered";
            return "Validator is on";
        }

        /// <summary>
        /// WithdrawStake:
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>tx hash</returns>
        public string WithdrawStake(string[] arguments)
        {
            if (!_validatorStatusManager.IsStarted())
                return "ERROR: Validator is off";
            if (_validatorStatusManager.IsWithdrawTriggered())
                return "ERROR: Stake withdraw is triggered already";
            _validatorStatusManager.WithdrawStakeAndStop();
            return "Stake withdraw is initiated";
        }

        public string Debug(string[] arguments)
        {
            using (DataTarget dataTarget = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, suspend: false))
            {
                ClrInfo version = dataTarget.ClrVersions[0];
                using ClrRuntime runtime = version.CreateRuntime();
                // Walk each thread in the process.
                foreach (ClrThread thread in runtime.Threads)
                {
                    // The ClrRuntime.Threads will also report threads which have recently died, but their
                    // underlying datastructures have not yet been cleaned up.  This can potentially be
                    // useful in debugging (!threads displays this information with XXX displayed for their
                    // OS thread id).  You cannot walk the stack of these threads though, so we skip them
                    // here.
                    if (!thread.IsAlive)
                        continue;

                    Console.WriteLine("Thread {0:X}:", thread.OSThreadId);

                    // Each thread tracks a "last thrown exception".  This is the exception object which
                    // !threads prints.  If that exception object is present, we will display some basic
                    // exception data here.  Note that you can get the stack trace of the exception with
                    // ClrHeapException.StackTrace (we don't do that here).
                    ClrException? currException = thread.CurrentException;
                    if (currException is ClrException ex)
                        Console.WriteLine("Exception: {0:X} ({1}), HRESULT={2:X}", ex.Address, ex.Type.Name, ex.HResult);

                    // Walk the stack of the thread and print output similar to !ClrStack.
                    Console.WriteLine();
                    Console.WriteLine("Managed Callstack:");
                    foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
                    {
                        // Note that CLRStackFrame currently only has three pieces of data: stack pointer,
                        // instruction pointer, and frame name (which comes from ToString).  Future
                        // versions of this API will allow you to get the type/function/module of the
                        // method (instead of just the name).  This is not yet implemented.
                        Console.WriteLine($"    {frame.StackPointer:x12} {frame.InstructionPointer:x12} {frame}");
                    }

                    // Print a !DumpStackObjects equivalent.
                    {
                        // We'll need heap data to find objects on the stack.
                        ClrHeap heap = runtime.Heap;

                        // Walk each pointer aligned address on the stack.  Note that StackBase/StackLimit
                        // is exactly what they are in the TEB.  This means StackBase > StackLimit on AMD64.
                        ulong start = thread.StackBase;
                        ulong stop = thread.StackLimit;

                        // We'll walk these in pointer order.
                        if (start > stop)
                        {
                            ulong tmp = start;
                            start = stop;
                            stop = tmp;
                        }

                        Console.WriteLine();
                        Console.WriteLine("Stack objects:");

                        // Walk each pointer aligned address.  Ptr is a stack address.
                        for (ulong ptr = start; ptr <= stop; ptr += (uint)IntPtr.Size)
                        {
                            // Read the value of this pointer.  If we fail to read the memory, break.  The
                            // stack region should be in the crash dump.
                            if (!dataTarget.DataReader.ReadPointer(ptr, out ulong obj))
                                break;

                            // 003DF2A4 
                            // We check to see if this address is a valid object by simply calling
                            // GetObjectType.  If that returns null, it's not an object.
                            ClrType type = heap.GetObjectType(obj);
                            if (type == null)
                                continue;

                            // Don't print out free objects as there tends to be a lot of them on
                            // the stack.
                            if (!type.IsFree)
                                Console.WriteLine("{0,16:X} {1,16:X} {2}", ptr, obj, type.Name);
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("----------------------------------");
                    Console.WriteLine();
                }
            }
            return "********\n";
        }

    }
}