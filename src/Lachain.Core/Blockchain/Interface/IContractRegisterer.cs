using System;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IContractRegisterer
    {
        Type? GetContractByAddress(UInt160 address);

        SystemContractCall? DecodeContract(ContractContext context, UInt160 address, byte[] input);
    }
}