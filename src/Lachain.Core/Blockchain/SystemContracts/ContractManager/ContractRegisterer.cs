using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.VM;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts.ContractManager
{
    public class ContractRegisterer : IContractRegisterer
    {
        private readonly ConcurrentDictionary<UInt160, Type> _contracts
            = new ConcurrentDictionary<UInt160, Type>();

        private readonly Dictionary<UInt160, Dictionary<uint, Tuple<string, MethodInfo>>> _signatures
            = new Dictionary<UInt160, Dictionary<uint, Tuple<string, MethodInfo>>>();

        public static readonly UInt160 DeployContract = new BigInteger(0).ToUInt160();
        public static readonly UInt160 LatokenContract = new BigInteger(1).ToUInt160();
        public static readonly UInt160 GovernanceContract = new BigInteger(2).ToUInt160();

        public ContractRegisterer()
        {
            /* address <<0x0>> references contract to deploy other contracts */
            RegisterContract<DeployContract>(DeployContract);
            /* address <<0x1>> references LaToken contract */
            RegisterContract<BasicLaTokenContract>(LatokenContract);
            /* address <<0x2>> references Governance contract */
            RegisterContract<GovernanceContract>(GovernanceContract);
        }

        public void RegisterContract<T>(UInt160 address)
            where T : ISystemContract
        {
            if (!_contracts.TryAdd(address, typeof(T)))
                throw new System.Exception("Failed to register system contract at address (" + address.ToHex() + ")");
            var signatures = typeof(T).FindAttributes<ContractMethodAttribute>()
                .Select(tuple => Tuple.Create(tuple.Item2.Method, tuple.Item1)).ToList();
            var props = typeof(T).FindAttributes<ContractPropertyAttribute>()
                .Select(tuple => Tuple.Create(tuple.Item2.Property, tuple.Item1)).ToList();
            _signatures.Add(address,
                signatures.Concat(props)
                    .ToDictionary(
                        tuple => ContractEncoder.MethodSignatureAsInt(tuple.Item1),
                        tuple => tuple
                    )
            );
        }

        public Type? GetContractByAddress(UInt160 address)
        {
            return _contracts.TryGetValue(address, out var result) ? result : null;
        }

        public Tuple<Type, MethodInfo, object[]>? DecodeContract(UInt160 address, byte[] input)
        {
            if (input.Length < 4)
                throw new ArgumentOutOfRangeException(nameof(input));
            if (!_contracts.TryGetValue(address, out var contract) || !_signatures.TryGetValue(address, out var signatures))
                return null;
            var signature = ContractEncoder.MethodSignatureAsInt(input);
            if (!signatures.TryGetValue(signature, out var tuple))
                return null;
            var (methodName, methodInfo) = tuple;
            var decoder = new ContractDecoder(input);
            return Tuple.Create(contract, methodInfo, decoder.Decode(methodName));
        }
    }
}