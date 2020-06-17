using System;
using System.Linq;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts.Utils
{
    public class SystemContractReader: ISystemContractReader
    {
        private readonly IStateManager _stateManager;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly UInt160 _nodeAddress;
        private readonly byte[] _nodePublicKey;

        public SystemContractReader(IStateManager stateManager, IPrivateWallet privateWallet, IContractRegisterer contractRegisterer)
        {
            _stateManager = stateManager;
            _contractRegisterer = contractRegisterer;
            _nodeAddress = privateWallet.EcdsaKeyPair.PublicKey.GetAddress();
            _nodePublicKey = privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray();
        }

        private byte[] ReadSystemContractData(UInt160 contractAddress, string method, params dynamic[] values)
        {
            var snapshot = _stateManager.LastApprovedSnapshot;
            
            var context = new InvocationContext(_nodeAddress, snapshot, new TransactionReceipt
            {
                Block = snapshot.Blocks.GetTotalBlockHeight(),
            });
            var input = ContractEncoder.Encode(method, values);
            var call = _contractRegisterer.DecodeContract(context, contractAddress, input);
            if (call is null) throw new Exception("System contract invocation failed");
            
            var result = VirtualMachine.InvokeSystemContract(call, context, input, 100_000_000);
            
            if (result.Status != ExecutionStatus.Ok) 
                throw new Exception("System contract failed");
            
            return result.ReturnValue!;
        }

        public UInt256 GetStake(UInt160 stakerAddress = null)
        {
            stakerAddress ??= _nodeAddress;
            var invResult = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                stakerAddress);

            return invResult.ToUInt256();
        }

        public UInt256 GetPenalty(UInt160 stakerAddress = null)
        {
            stakerAddress ??= _nodeAddress;
            var invResult = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetPenalty,
                stakerAddress);

            return invResult.ToUInt256();
        }

        public UInt256 GetTotalStake()
        {
            var invResult = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetTotalActiveStake);
            return invResult.ToUInt256();
        }

        public byte[] GetVRFSeed()
        {
            return ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetVrfSeed);
        }

        public int GetWithdrawRequestCycle(UInt160 stakerAddress = null)
        {
            stakerAddress ??= _nodeAddress;
            var res = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle,
                stakerAddress);
            return res.Length > 0 ? BitConverter.ToInt32(res): 0;
        }



        public bool IsNextValidator(byte[] stakerPublicKey = null)
        {
            stakerPublicKey ??= _nodePublicKey;
            if (IsVrfSubmissionPhase())
            {
                var isNextValidatorStaking = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodIsNextValidator,
                    stakerPublicKey);
                return !isNextValidatorStaking.ToUInt256().IsZero();
            }
            var isNextValidatorGovernance = ReadSystemContractData(ContractRegisterer.GovernanceContract, GovernanceInterface.MethodIsNextValidator,
                stakerPublicKey);
            
            return !isNextValidatorGovernance.ToUInt256().IsZero();
        }
        
        public bool IsVrfSubmissionPhase()
        {
            var blockNumber = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle < StakingContract.VrfSubmissionPhaseDuration;
        }
        
        public bool IsAttendanceDetectionPhase()
        {
            var blockNumber = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle < StakingContract.AttendanceDetectionDuration;
        }
        
        public bool IsKeyGenPhase()
        {
            var blockNumber = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle >= StakingContract.VrfSubmissionPhaseDuration;
        }
        
        public bool IsCheckedIn(byte[] stakerPublicKey = null)
        {
            stakerPublicKey ??= _nodePublicKey;
            var isCheckedIn = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodIsCheckedInAttendanceDetection,
                stakerPublicKey);
            return !isCheckedIn.ToUInt256().IsZero();
        }
        
        public bool IsPreviousValidator(byte[] stakerPublicKey = null)
        {
            stakerPublicKey ??= _nodePublicKey;
            var isPreviousValidator = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodIsPreviousValidator,
                stakerPublicKey);
            
            return !isPreviousValidator.ToUInt256().IsZero();
        }
        
        public byte[][] GetPreviousValidators()
        {
            var previousValidatorsData = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodGetPreviousValidators);
            
            byte[][] validators = {};
            for (var startByte = 0; startByte < previousValidatorsData.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = previousValidatorsData.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validator}).ToArray();
            }
            return validators;
        }
        
        public bool IsAbleToBeValidator(UInt160 stakerAddress = null)
        {
            stakerAddress ??= _nodeAddress;
            var isAble = ReadSystemContractData(ContractRegisterer.StakingContract, StakingInterface.MethodIsAbleToBeValidator,
                stakerAddress);

            return !isAble.ToUInt256().IsZero();
        }
        
        public UInt160 NodeAddress()
        {
            return _nodeAddress;
        }
        
        public byte[] NodePublicKey()
        {
            return _nodePublicKey;
        }
    }
}