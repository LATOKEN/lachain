using System;
using System.Reflection;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IContractRegisterer
    {
        void RegisterContract<T>(UInt160 address)
            where T : ISystemContract;
        
        Type? GetContractByAddress(UInt160 address);

        Tuple<Type, MethodInfo, object[]>? DecodeContract(UInt160 address, byte[] input);
    }
}