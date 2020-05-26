using System;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IContractRegisterer
    {
        Type? GetContractByAddress(UInt160 address);

        SystemContractCall? DecodeContract(InvocationContext context, UInt160 address, byte[] input);
    }
}