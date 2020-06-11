using System;
using System.Linq;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts.Utils
{
    public static class Hepler
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
     
        public static UInt160 PublicKeyToAddress(byte[] publicKey)
        {
            return Crypto.ComputeAddress(publicKey).ToUInt160();
        }
     
        public static InvocationResult CallSystemContract(SystemContractExecutionFrame currentFrame, UInt160 contractAddress, UInt160 sender, string methodSignature, params dynamic[] values)
        {
            var context = currentFrame.InvocationContext.NextContext(sender);
            var input = ContractEncoder.Encode(methodSignature, values);
            // TODO: pass correct gas amount
            return ContractInvoker.Invoke(contractAddress, context, input, 100_000_000);
        }
    }
}