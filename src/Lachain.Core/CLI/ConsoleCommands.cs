using System;
using System.Collections.Generic;
using System.Linq;
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
            return hexString.All(currentCharacter =>
                currentCharacter >= '0' && currentCharacter <= '9' ||
                currentCharacter >= 'a' && currentCharacter <= 'f' ||
                currentCharacter >= 'A' && currentCharacter <= 'F');
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

            var tx = _transactionManager.GetByHash(arguments[1].HexToUInt256());
            return tx is null ? $"Transaction {arguments[1]} not found" : ProtoUtils.ParsedObject(tx);
        }

        /*
         * GetBlock
         * blockHash, UInt256
        */
        public string? GetBlock(string[] arguments)
        {
            if (arguments.Length != 2) return "Wrong number of arguments";
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
            if (arguments.Length != 2) return null;
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1])) return null;
            return _stateManager.LastApprovedSnapshot.Balances
                .GetBalance(arguments[1].HexToUInt160());
        }

        public string DeployContract(string[] arguments)
        {
            var from = _keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var hash = from.ToBytes().Concat(nonce.ToBytes()).Keccak();
            var byteCode = arguments[1].HexToBytes();
            if (!VirtualMachine.VerifyContract(byteCode)) return "Unable to validate smart-contract code";
            Console.WriteLine("Contract Hash: " + hash.ToHex());
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = _transactionSigner.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.ToHex();
        }

        public string CallContract(string[] arguments)
        {
            return ""; // TODO: implement it properly
        }

        public string Help(string[] arguments)
        {
            var methods = GetType().GetMethods().SelectMany(m =>
            {
                if (new string[] {"gettype", "equals", "tostring", "gethashcode"}.Contains(m.Name.ToLower()))
                    return Enumerable.Empty<string>();
                var args = m.GetParameters().Select(arg => $"{arg.ParameterType.Name} {arg.Name}");
                return new[] {$" - {m.Name.ToLower()}: {string.Join(", ", args)}"};
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
            return $"{tx.FullHash(sig).ToHex()}";
        }

        /*
         * SendTransaction:
         * 1. to, UInt160,
         * 2. value, UInt256,
         * 3. gasPrice, UInt256
        */
        public string SendTransaction(string[] arguments)
        {
            var to = arguments[1].HexToUInt160();
            var value = Money.Parse(arguments[2]);
            var gasPrice = ulong.Parse(arguments[3]);
            var from = _keyPair.PublicKey.GetAddress();
            var tx = _transactionBuilder.TransferTransaction(from, to, value, gasPrice);
            if (gasPrice == 0) Console.WriteLine($"Set recommended gas price: {tx.GasPrice}");
            var signedTx = _transactionSigner.Sign(tx, _keyPair);
            var error = _transactionPool.Add(signedTx);
            return error != OperatingError.Ok
                ? $"Error adding tx {signedTx.Hash.ToHex()} to pool: {error}"
                : signedTx.Hash.ToHex();
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
            var tx = Transaction.Parser.ParseFrom(arguments[1].HexToBytes());
            var sig = arguments[2].HexToBytes().ToSignature();
            var accepted = new TransactionReceipt
            {
                Transaction = tx,
                Hash = tx.FullHash(sig),
                Signature = sig
            };
            var txValid = _transactionManager.Verify(accepted) == OperatingError.Ok;
            var sigValid = _transactionManager.VerifySignature(accepted) == OperatingError.Ok;
            Console.WriteLine($"Tx Hash: {tx.FullHash(sig)}");
            Console.WriteLine("Transaction validity: " + txValid);
            Console.WriteLine(sigValid ? "Signature validity: OK" : "Signature validity: INVALID");
            return $"{txValid && sigValid}";
        }

        /// <summary>
        /// CurrentStake:
        ///  outputs current stake size
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>current stake size</returns>
        public string CurrentStake(string[] arguments)
        {
            return $"{_systemContractReader.GetStake().ToMoney()}";
        }

        /// <summary>
        /// Stake:
        ///  stake amount
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>stake tx hash</returns>
        public string NewStake(string[] arguments)
        {
            if (_validatorStatusManager.IsStarted()) return "ERROR: Withdraw current stake first";
            if (arguments.Length == 0) _validatorStatusManager.Start(false);
            else _validatorStatusManager.StartWithStake(Money.Parse(arguments[0]).ToUInt256());
            return "Validator is started";
        }

        /// <summary>
        /// ValidatorStatus:
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>tx hash</returns>
        public string ValidatorStatus(string[] arguments)
        {
            if (!_validatorStatusManager.IsStarted()) return "Validator is off";
            if (_validatorStatusManager.IsWithdrawTriggered()) return "Stake withdraw is triggered";
            return "Validator is on";
        }

        /// <summary>
        /// WithdrawStake:
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>tx hash</returns>
        public string WithdrawStake(string[] arguments)
        {
            if (!_validatorStatusManager.IsStarted()) return "ERROR: Validator is off";
            if (_validatorStatusManager.IsWithdrawTriggered()) return "ERROR: Stake withdraw is triggered already";
            _validatorStatusManager.WithdrawStakeAndStop();
            return "Stake withdraw is initiated";
        }

        /// <summary>
        /// Pprof:
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>pprof URL</returns>
        public string Pprof(string[] arguments)
        {
            return $"http://localhost:{CommunicationHub.Net.Hub.GetProfilerPort()}/debug/pprof/";
        }
    }
}