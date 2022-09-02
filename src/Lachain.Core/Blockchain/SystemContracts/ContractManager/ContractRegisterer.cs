using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.VM;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts.ContractManager
{
    public class ContractRegisterer : IContractRegisterer
    {
        private static readonly ILogger<ContractRegisterer> Logger =
            LoggerFactory.GetLoggerForClass<ContractRegisterer>();

        private readonly ConcurrentDictionary<UInt160, Type> _contracts
            = new ConcurrentDictionary<UInt160, Type>();

        private readonly IDictionary<UInt160, Dictionary<uint, (string methodName, MethodInfo methodInfo)>> _signatures
            = new Dictionary<UInt160, Dictionary<uint, (string methodName, MethodInfo methodInfo)>>();

        public static readonly UInt160 DeployContract = new BigInteger(0).ToUInt160();
        public static readonly UInt160 LatokenContract = new BigInteger(1).ToUInt160();
        public static readonly UInt160 GovernanceContract = new BigInteger(2).ToUInt160();
        public static readonly UInt160 StakingContract = new BigInteger(3).ToUInt160();
        public static readonly UInt160 NativeTokenContract = new BigInteger(4).ToUInt160();

        public ContractRegisterer()
        {
            /* address <<0x0>> references contract to deploy other contracts */
            RegisterContract<DeployContract>(DeployContract);
            /* address <<0x1>> references LaToken contract */
            RegisterContract<NativeTokenContract>(LatokenContract);
            /* address <<0x2>> references Governance contract */
            RegisterContract<GovernanceContract>(GovernanceContract);
            /* address <<0x3>> references Staking contract */
            RegisterContract<StakingContract>(StakingContract);
            /* address <<0x4>> references Staking contract */
            RegisterContract<NativeTokenContract>(NativeTokenContract);
        }

        private void RegisterContract<T>(UInt160 address)
            where T : ISystemContract
        {
            if (!_contracts.TryAdd(address, typeof(T)))
                throw new System.Exception("Failed to register system contract at address (" + address.ToHex() + ")");
            var signatures = typeof(T)
                .FindAttributes<ContractMethodAttribute>()
                .ToList();
            _signatures.Add(address,
                signatures
                    .ToDictionary(
                        t => ContractEncoder.MethodSignatureAsInt(t.attribute.Method),
                        t => (t.attribute.Method, t.method)
                    )
            );
        }

        public Type? GetContractByAddress(UInt160 address)
        {
            return _contracts.TryGetValue(address, out var result) ? result : null;
        }

        public string? GetMethodName(UInt160 address, byte[] input)
        {
            if (!_signatures.TryGetValue(address, out var signatures))
                return null;
            
            var signature = ContractEncoder.MethodSignatureAsInt(input);
            if (!signatures.TryGetValue(signature, out var tuple))
                return null;
                
            var (methodName, _) = tuple;
            return methodName;
        }

        public SystemContractCall? DecodeContract(InvocationContext context, UInt160 address, byte[] input)
        {
            if (input.Length < 4)
            {
                Logger.LogWarning("Input too small");
                return null;
            }

            if (!_contracts.TryGetValue(address, out var contract) ||
                !_signatures.TryGetValue(address, out var signatures)
               )
            {
                Logger.LogWarning("Failed to get signatures");
                return null;
            }

            var signature = ContractEncoder.MethodSignatureAsInt(input);
            if (!signatures.TryGetValue(signature, out var tuple))
            {
                Logger.LogWarning("Failed to get method info");
                return null;
            }

            var (methodName, methodInfo) = tuple;
            Logger.LogDebug($"Method {methodName}");
            var decoder = new ContractDecoder(input);
            var instance = Activator.CreateInstance(contract, context);
            try
            {
                return new SystemContractCall(instance, methodInfo, decoder.Decode(methodName), address);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception {ex} while call construction");
                return null;
            }
        }
    }
}