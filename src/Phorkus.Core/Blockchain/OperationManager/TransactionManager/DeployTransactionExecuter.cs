using System;
using System.Linq;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Core.VM;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class DeployTransactionExecuter : ITransactionExecuter
    {
        private readonly IVirtualMachine _virtualMachine;
        
        public DeployTransactionExecuter(IVirtualMachine virtualMachine)
        {
            _virtualMachine = virtualMachine;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* validate transaction before execution */
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var deploy = transaction.Deploy;
            /* calculate contract hash and register it */
            var hash = transaction.From.Buffer.ToArray().Concat(BitConverter.GetBytes((uint) transaction.Nonce)).ToHash160();
            Console.WriteLine("Contract hash: " + hash.Buffer.ToHex());
            var contract = new Contract
            {
                Hash = hash,
                Version = deploy.Version,
                Abi = { deploy.Abi },
                Wasm = deploy.Wasm
            };
            if (!_virtualMachine.VerifyContract(contract.Wasm.ToByteArray()))
                return OperatingError.InvalidContract;
            snapshot.Contracts.AddContract(transaction.From, contract);
            return OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Deploy)
                return OperatingError.InvalidTransaction;
            var deploy = transaction.Deploy;
            if (deploy?.Version is null)
                return OperatingError.InvalidTransaction;
            // TODO: abi
            if (deploy?.Wasm is null)
                return OperatingError.InvalidTransaction;
            var result = _VerifyContract(transaction);
            if (result != OperatingError.Ok)
                return result;
            return result;
        }

        private OperatingError _VerifyContract(Transaction transaction)
        {
            /* TODO: "validate opcodes here" */
            return OperatingError.Ok;
        }
    }
}