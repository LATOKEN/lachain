using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.ContractManager.Attributes;
using Lachain.Core.Blockchain.OperationManager.SystemContracts;
using Lachain.Core.VM;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.ContractManager
{
    public class ContractRegisterer : IContractRegisterer
    {        
        private readonly ConcurrentDictionary<UInt160, Type> _contracts
            = new ConcurrentDictionary<UInt160, Type>();
        
        private readonly Dictionary<UInt160, Dictionary<uint, Tuple<string, MethodInfo>>> _signatures
            = new Dictionary<UInt160, Dictionary<uint, Tuple<string, MethodInfo>>>();

        public ContractRegisterer()
        {
            /* zero address references to LaToken contract */
            RegisterContract<BasicLaTokenContract>(UInt160Utils.Zero);
        }
        
        public void RegisterContract<T>(UInt160 address)
            where T : ISystemContract
        {
            if (!_contracts.TryAdd(address, typeof(T)))
                throw new Exception("Failed to reigster system contract at address (" + address.ToHex() + ")");
            var signatures = typeof(T).FindAttributes<ContractMethodAttribute>().Select(tuple => Tuple.Create(tuple.Item2.Method, tuple.Item1)).ToList();
            var props = typeof(T).FindAttributes<ContractPropertyAttribute>().Select(tuple => Tuple.Create(tuple.Item2.Property, tuple.Item1)).ToList();
            _signatures.Add(address, signatures.Concat(props).ToDictionary(tuple => BitConverter.ToUInt32(ContractEncoder.Encode(tuple.Item1), 0), tuple => tuple));
        }

        public Type? GetContractByAddress(UInt160 address)
        {
            return _contracts.TryGetValue(address, out var result) ? result : null;
        }
        
        public Tuple<Type, MethodInfo, object[]>? DecodeContract(UInt160 address, byte[] input)
        {
            if (input.Length < 4)
                throw new ArgumentOutOfRangeException(nameof(input));
            if (!_contracts.TryGetValue(address, out var contract) || !_signatures.TryGetValue(address, out var sigs))
                return null;
            var signature = BitConverter.ToUInt32(input, 0);
            if (!sigs.TryGetValue(signature, out var tuple))
                return null;
            var(methodName, methodInfo) = tuple;
            var decoder = new ContractDecoder(input);
            return Tuple.Create(contract, methodInfo, decoder.Decode(methodName));
        }
    }
}