using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Crypto.VRF;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using NetMQ;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class StakingContract : ISystemContract
    {
        
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private readonly InvocationContext _context;
        public static readonly ulong CycleDuration = 1000; // in blocks
        public static readonly ulong SubmissionPhaseDuration = 500; // in blocks
        public static readonly ulong AttendanceDetectionDuration = 100; // in blocks
        public static readonly BigInteger TokenUnitsInRoll = BigInteger.Pow(10, 21);
        private readonly StorageVariable _nextValidators; // array of public keys
        private readonly StorageVariable _previousValidators; // array of public keys
        private readonly StorageVariable _attendancetDetectorCheckIns; // array of public keys
        private readonly StorageVariable _stakers; // array of public keys
        private readonly StorageVariable _vrfSeed;
        private readonly StorageVariable _nextVrfSeed;
        public static readonly byte[] Role = Encoding.ASCII.GetBytes("staker");
        public static readonly BigInteger ExpectedValidatorsCount = 22;
        private readonly StorageMapping _userToStakerId;
        private readonly StorageMapping _userToPubKey;
        private readonly StorageMapping _userToStake;
        private readonly StorageMapping _userToStartCycle;
        private readonly StorageMapping _userToWithdrawRequestCycle;
        private readonly StorageMapping _attendanceVotes;

        private static readonly ILogger<StakingContract> Logger =
            LoggerFactory.GetLoggerForClass<StakingContract>();

        public StakingContract(InvocationContext contractContext)
        {
            _context = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _nextVrfSeed = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                BigInteger.Zero.ToUInt256()
            );
            _userToStakerId = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(1).ToUInt256()
            );
            _userToPubKey = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(2).ToUInt256()
            );
            _userToStake = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(3).ToUInt256()
            );
            _userToStartCycle = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(4).ToUInt256()
            );
            _userToWithdrawRequestCycle = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(5).ToUInt256()
            );
            _stakers = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(6).ToUInt256()
            );
            _vrfSeed = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(7).ToUInt256()
            );
            _nextValidators = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(8).ToUInt256()
            );
            _previousValidators = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(9).ToUInt256()
            );
            _attendancetDetectorCheckIns = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(10).ToUInt256()
            );
            _attendanceVotes = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(11).ToUInt256()
            );
            TryInitStoarge();
        }

        public ContractStandard ContractStandard => ContractStandard.StakingContract;

        [ContractMethod(StakingInterface.MethodBecomeStaker)]
        public ExecutionStatus BecomeStaker(byte[] publicKey, UInt256 amount, SystemContractExecutionFrame frame)
        {
            
            frame.UseGas(GasMetering.StakingBecomeStakerCost);
            
            var ok = IsPublicKeyOwner(publicKey, MsgSender());
            if (!ok)
                return ExecutionStatus.ExecutionHalted;
            
            if (amount.ToBigInteger() < TokenUnitsInRoll)
                return ExecutionStatus.ExecutionHalted;

            var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                MsgSender());
            
            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var stake = getStakeExecutionResult.ReturnValue.ToUInt256();
            if (!stake.IsZero())
                return ExecutionStatus.ExecutionHalted;
            
            var balanceOfExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, ContractRegisterer.StakingContract, Lrc20Interface.MethodBalanceOf,
                MsgSender());
            
            if (balanceOfExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var balance = balanceOfExecutionResult.ReturnValue.ToUInt256().ToMoney();
            
            if (balance.CompareTo(amount.ToMoney()) == -1)
                return ExecutionStatus.ExecutionHalted;
            
            var transferExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, MsgSender(), Lrc20Interface.MethodTransfer,
                ContractRegisterer.StakingContract, amount);
            
            if (transferExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var fail = transferExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (fail)
                return ExecutionStatus.ExecutionHalted;

            var startingCycle = GetCurrentCycle() + 1;
            
            // Special case for initial validator
            if (startingCycle == 1)
            {
                startingCycle--;
            }

            SetStake(MsgSender(), amount);
            
            SetStakerPublicKey(MsgSender(), publicKey);
            
            SetStartCycle(MsgSender(), startingCycle);
            
            var id = AddStaker(publicKey);
            SetStakerId(MsgSender(), id);
            
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodRequestStakeWithdrawal)]
        public ExecutionStatus RequestStakeWithdrawal(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingRequestStakeWithdrawalCost);
            
            var ok = IsPublicKeyOwner(publicKey, MsgSender());
            if (!ok) return ExecutionStatus.ExecutionHalted;

            var isNextvalidatorExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsNextValidator, publicKey);

            if (isNextvalidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var isNextValidator = !isNextvalidatorExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (isNextValidator)
                return ExecutionStatus.ExecutionHalted;
            
            var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, MsgSender());

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var stake = getStakeExecutionResult.ReturnValue.ToUInt256();

            if (stake.IsZero())
                return ExecutionStatus.ExecutionHalted;
            
            var getWithdrawRequestCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, MsgSender());

            if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;

            if (withdrawRequestCycleBytes.Length != 0)
                return ExecutionStatus.ExecutionHalted;
            
            SetWithdrawRequestCycle(MsgSender(), GetCurrentCycle());
            
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodWithdrawStake)]
        public ExecutionStatus WithdrawStake(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingWithdrawStakeCost);
            
            var ok = IsPublicKeyOwner(publicKey, MsgSender());
            if (!ok) return ExecutionStatus.ExecutionHalted;
            
            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle < AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;

            var getWithdrawRequestCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, MsgSender());

            if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;
            
            if (withdrawRequestCycleBytes.Length != 0)
                return ExecutionStatus.ExecutionHalted;

            var withdrawRequestCycle = BitConverter.ToInt32(withdrawRequestCycleBytes);
            
            if (withdrawRequestCycle == GetCurrentCycle())
                return ExecutionStatus.ExecutionHalted;

            var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, MsgSender());

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var stake = getStakeExecutionResult.ReturnValue.ToUInt256();
            
            if (stake.IsZero())
                return ExecutionStatus.ExecutionHalted;

            var transferExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, ContractRegisterer.StakingContract, Lrc20Interface.MethodTransfer,
                MsgSender(), stake);
            
            if (transferExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var fail = transferExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (fail)
                return ExecutionStatus.ExecutionHalted;

            DeleteStaker(MsgSender());
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodSubmitVrf)]
        public ExecutionStatus SubmitVrf(byte[] publicKey, byte[] proof, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingSubmitVrfCost);
            
            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= SubmissionPhaseDuration)
                return ExecutionStatus.ExecutionHalted;
            
            var ok = IsPublicKeyOwner(publicKey, MsgSender());
            if (!ok) return ExecutionStatus.ExecutionHalted;

            var isNextValidatorExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsNextValidator, publicKey);

            if (isNextValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var isNextValidator = !isNextValidatorExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (isNextValidator)
                return ExecutionStatus.ExecutionHalted;

            var isAbleToBeAValidatorExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, MsgSender(), StakingInterface.MethodIsAbleToBeAValidator, MsgSender());

            if (isAbleToBeAValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var isAbleToBeAValidator = !isAbleToBeAValidatorExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (!isAbleToBeAValidator)
                return ExecutionStatus.ExecutionHalted;
            
            var getTotalStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetTotalActiveStake);

            if (getTotalStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var totalStake = getTotalStakeExecutionResult.ReturnValue.ToUInt256().ToBigInteger();
            var totalRolls = totalStake / TokenUnitsInRoll;
            
            var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, MsgSender());

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var stake = getStakeExecutionResult.ReturnValue.ToUInt256().ToBigInteger();
            
            var getVrfSeedExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetVrfSeed);

            if (getVrfSeedExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var vrfSeed = getVrfSeedExecutionResult.ReturnValue;
            var isWinner = Vrf.IsWinner(
                publicKey,
                proof,
                vrfSeed,
                Role,
                ExpectedValidatorsCount,
                stake / TokenUnitsInRoll,
                totalRolls
            );

            if (!isWinner)
                return ExecutionStatus.ExecutionHalted;
            
            SetNextValidator(publicKey);
            TrySetNextVrfSeed(Vrf.ProofToHash(proof));
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodSubmitAttendanceDetection)]
        public ExecutionStatus SubmitAttendanceDetection(byte[][] faultPersons, UInt256[] faultBlocksCounts, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingSubmitAttendanceDetectionCost);
            
            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;
            
            if (faultPersons.Length != faultBlocksCounts.Length)
                return ExecutionStatus.ExecutionHalted;
            
            
            var senderPublicKey = _userToPubKey.GetValue(MsgSender().ToBytes());
            if (senderPublicKey.Length == 0)
                return ExecutionStatus.ExecutionHalted;

            var isSenderPreviousValidatorExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsPreviousValidator, senderPublicKey);

            if (isSenderPreviousValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var isSenderPreviousValidator = !isSenderPreviousValidatorExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (!isSenderPreviousValidator)
                return ExecutionStatus.ExecutionHalted;
                
            var isCheckedInAttendanceDetectionExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsCheckedInAttendanceDetection, senderPublicKey);

            if (isCheckedInAttendanceDetectionExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var isCheckedInAttendanceDetection = !isCheckedInAttendanceDetectionExecutionResult.ReturnValue.ToUInt256().IsZero();
            if (isCheckedInAttendanceDetection)
                return ExecutionStatus.ExecutionHalted;

            _attendancetDetectorCheckIns.Set(_attendancetDetectorCheckIns.Get().Concat(senderPublicKey).ToArray());

            for (var i = 0; i < faultPersons.Length; i++)
            {
                var isPreviousValidatorExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsPreviousValidator, faultPersons[i]);

                if (isPreviousValidatorExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;
            
                var isPreviousValidator = !isPreviousValidatorExecutionResult.ReturnValue.ToUInt256().IsZero();
                if (!isPreviousValidator)
                    return ExecutionStatus.ExecutionHalted;
                
                var detectedBlocks = faultBlocksCounts[i].ToBigInteger();
                
                if (detectedBlocks > CycleDuration)
                    return ExecutionStatus.ExecutionHalted;
                
                VoteForAttendance(faultPersons[i], (int) detectedBlocks);
            }
            return ExecutionStatus.Ok;
        }

        private void VoteForAttendance(byte[] faultPerson, int faultBlocksCount)
        {
            var votes = _attendanceVotes.GetValue(faultPerson);
            _attendanceVotes.SetValue(faultPerson, votes.Concat(faultBlocksCount.ToBytes().ToArray()).ToArray());
        }

        private void ClearAttendanceVotes(byte[] faultPerson)
        {
            _attendanceVotes.Delete(faultPerson);
        }

        private int[] GetAttendanceVotes(byte[] faultPerson)
        {
            var votesBytes = _attendanceVotes.GetValue(faultPerson);
            var votes = new int[votesBytes.Length / 4];
            for (var i = 0; i < votes.Length; i += 1)
            {
                votes[i] = votesBytes.AsReadOnlySpan().Slice(4 * i).ToInt32();
            }

            return votes;
        }

        private void ClearAttendanceDetectorCheckIns()
        {
           _attendancetDetectorCheckIns.Set(new byte[]{});
        }

        [ContractMethod(StakingInterface.MethodIsCheckedInAttendanceDetection)]
        public ExecutionStatus IsCheckedInAttendanceDetection(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingIsCheckedInAttendanceDetectionCost);
            
            var result = false;
            var seenValidatorsBytes = _attendancetDetectorCheckIns.Get();
            for (var startByte = 0; startByte < seenValidatorsBytes.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = seenValidatorsBytes.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    result = true;
                    break;
                }
            }

            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            
            return ExecutionStatus.Ok;
        }

        public ExecutionStatus DistributeRewardsAndPenalties(UInt256 totalReward, SystemContractExecutionFrame frame)
        {
            if (!MsgSender().Equals(ContractRegisterer.GovernanceContract))
                return ExecutionStatus.ExecutionHalted;

            if (_context.Receipt.Block % CycleDuration != AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;
            
            var validatorsData = _previousValidators.Get();
            var validatorsCount = validatorsData.Length / CryptoUtils.PublicKeyLength;
            var maxValidatorReward = totalReward.ToMoney() / validatorsCount; 
            for (var i = 0; i < validatorsCount; i ++)
            {
                var validator = validatorsData.Skip(i * CryptoUtils.PublicKeyLength).Take(CryptoUtils.PublicKeyLength).ToArray();
                
                var isCheckedInAttendanceDetectionExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodIsCheckedInAttendanceDetection, validator);

                if (isCheckedInAttendanceDetectionExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;
            
                var notCheckedIn = isCheckedInAttendanceDetectionExecutionResult.ReturnValue.ToUInt256().IsZero();
                if (notCheckedIn)
                {
                    /* Penalty for the cycle */
                    
                    var validatorAddress = SystemContractUtils.PublicKeyToAddress(validator);
                    var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                        ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, validatorAddress);

                    if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                        return ExecutionStatus.ExecutionHalted;
            
                    var stake = getStakeExecutionResult.ReturnValue.ToUInt256().ToMoney();
            
                    // TODO: determine correct number
                    var penalty = stake / 2;
                    if (penalty > Money.Zero)
                    {

                        SetStake(validatorAddress, (stake - penalty).ToUInt256());
                        _context.Snapshot.Balances.SubBalance(
                            ContractRegisterer.StakingContract, penalty
                        );
                    }
                }
                else
                {
                    var activeBlocks = GetActiveBlocksCount(validator);
                    var validatorReward = maxValidatorReward * activeBlocks / (int) CycleDuration;
                    if (validatorReward > Money.Zero)
                    {
                        _context.Snapshot.Balances.AddBalance(
                            SystemContractUtils.PublicKeyToAddress(validator), validatorReward
                        );
                    }
                }
                ClearAttendanceVotes(validator);
            }

            ClearAttendanceDetectorCheckIns();
            return ExecutionStatus.Ok;
        }

        private int GetActiveBlocksCount(byte[] validator)
        {
            var votes = GetAttendanceVotes(validator);
            if (votes.Length == 0) return 0;
            var middleIndex = votes.Length / 2;
            if (votes.Length % 2 == 0)
            {
                var median1 = QuickSelect(votes, middleIndex);
                var median2 = QuickSelect(votes, middleIndex + 1);
                
                return (median1 + median2) / 2;
            }

            return QuickSelect(votes, middleIndex);
        }


        [ContractMethod(StakingInterface.MethodFinishVrfLottery)]
        public ExecutionStatus FinishVrfLottery(SystemContractExecutionFrame frame)
        {
            if (!MsgSender().IsZero())
                return ExecutionStatus.ExecutionHalted;

            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle != CycleDuration / 2)
                return ExecutionStatus.ExecutionHalted;
            
            var nextValidators = _nextValidators.Get();
            
            if (nextValidators.Length == 0)
                return ExecutionStatus.ExecutionHalted; 

            _vrfSeed.Set(GetNextVrfSeed());
            _nextVrfSeed.Set(new byte[]{});
            _nextValidators.Set(new byte[]{});
            
            byte[][] validators = {};
            for (var startByte = 0; startByte < nextValidators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validatorPublicKey = nextValidators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validatorPublicKey}).ToArray();
            }

            var previousValidators = _context.Snapshot.Validators.GetValidatorsPublicKeys().Select(pk => pk.Buffer.ToByteArray());
            SetPreviousValidators(previousValidators.ToArray());

            var changeValidatorsExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.GovernanceContract, ContractRegisterer.StakingContract, GovernanceInterface.MethodChangeValidators, validators);

            if (changeValidatorsExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            return ExecutionStatus.Ok;
        }

        public int GetCurrentCycle()
        {
            return (int)(_context.Snapshot.Blocks.GetTotalBlockHeight() / CycleDuration);
        }

        [ContractMethod(StakingInterface.MethodGetVrfSeed)]
        public ExecutionStatus GetVrfSeed(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetVrfSeedCost);
            frame.ReturnValue = _vrfSeed.Get();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetNextVrfSeed)]
        public byte[] GetNextVrfSeed()
        {
            return _nextVrfSeed.Get();
        }

        [ContractMethod(StakingInterface.MethodIsNextValidator)]
        public ExecutionStatus IsNextValidator(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            var result = false;
            var validators = _nextValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    result = true;
                    break;
                }
            }

            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetPreviousValidators)]
        public ExecutionStatus GetPreviousValidators(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetPreviousValidators);
            var validatorsData = _previousValidators.Get();
            byte[][] validators = {};
            for (var startByte = 0; startByte < validatorsData.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validatorsData.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validator}).ToArray();
            }

            frame.ReturnValue = validatorsData;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodIsPreviousValidator)]
        public ExecutionStatus IsPreviousValidator(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingIsPreviousValidatorCost);
            
            var result = false;
            var validators = _previousValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (!validator.SequenceEqual(publicKey)) continue;
                result = true;
                break;
            }

            frame.ReturnValue = (result ? 1 : 0).ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        private void SetNextValidator(byte[] publicKey)
        {
            _nextValidators.Set(_nextValidators.Get().Concat(publicKey).ToArray());
        }

        private void SetPreviousValidators(byte[][] publicKeys)
        {
            _previousValidators.Set(publicKeys.Flatten().ToArray());
        }

        private void TrySetNextVrfSeed(byte[] vrfSeed)
        {
            var currentVrfSeedNum = GetNextVrfSeedNum();
            if (currentVrfSeedNum == 0 || vrfSeed.ToUInt256().ToBigInteger() < currentVrfSeedNum)
            {
                _nextVrfSeed.Set(vrfSeed);
            }
        }

        [ContractMethod(StakingInterface.MethodGetTotalActiveStake)]
        public ExecutionStatus GetTotalActiveStake(SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetTotalActiveStakeCost);
            
            Money stakeSum = UInt256Utils.Zero.ToMoney();
            var stakers = _stakers.Get();
            for (var startByte = CryptoUtils.PublicKeyLength; startByte < stakers.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var stakerPublicKey = stakers.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                var stakerAddress = SystemContractUtils.PublicKeyToAddress(stakerPublicKey);
                
                var getWithdrawRequestCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, stakerAddress);

                if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;
                
                var withdrawRequestCycle = withdrawRequestCycleBytes.Length > 0 ? BitConverter.ToInt32(withdrawRequestCycleBytes) : 0;
                
                var getStartCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, stakerAddress);

                if (getStartCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var startCycleBytes = getStartCycleExecutionResult.ReturnValue;
                
                var startCycle = startCycleBytes.Length > 0 ? BitConverter.ToInt32(startCycleBytes) : 0;
                
                if (GetCurrentCycle() >= startCycle
                    && (withdrawRequestCycle == 0
                        || withdrawRequestCycle == GetCurrentCycle()))
                {
                    var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                        ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, stakerAddress);

                    if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                        return ExecutionStatus.ExecutionHalted;
            
                    var stake = getStakeExecutionResult.ReturnValue.ToUInt256().ToMoney();
                    stakeSum += stake;
                }
            }

            frame.ReturnValue = stakeSum.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        
        [ContractMethod(StakingInterface.MethodIsAbleToBeAValidator)]
        public ExecutionStatus IsAbleToBeAValidator(UInt160 staker, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingIsAbleToBeAValidatorCost);

            var getStakeExecutionResult = SystemContractUtils.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake, MsgSender());
            
            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;
            
            var stake = getStakeExecutionResult.ReturnValue.ToUInt256();

            var notAStaker = stake.IsZero();
            if (notAStaker) 
                frame.ReturnValue = 0.ToUInt256().ToBytes();
            else
            {
                var getWithdrawRequestCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, staker);

                if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;
                
                var withdrawRequestCycle = withdrawRequestCycleBytes.Length > 0 ? BitConverter.ToInt32(withdrawRequestCycleBytes) : 0;
                
                var getStartCycleExecutionResult = SystemContractUtils.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetWithdrawRequestCycle, staker);

                if (getStartCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var startCycleBytes = getStartCycleExecutionResult.ReturnValue;
                
                var startCycle = startCycleBytes.Length > 0 ? BitConverter.ToInt32(startCycleBytes) : 0;
                
                frame.ReturnValue =
                    (GetCurrentCycle() >= startCycle && withdrawRequestCycle == 0 ? 1 : 0)
                    .ToUInt256().ToBytes();
            }

            return ExecutionStatus.Ok;
        }
        
        private void SetStake(UInt160 staker, UInt256 amount)
        {
            var key = staker.ToBytes();
            if (amount.IsZero())
            {
                return;
            }
            var value = amount.ToBytes();
            _userToStake.SetValue(key, value);
        }

        [ContractMethod(StakingInterface.MethodGetStake)]
        public ExecutionStatus GetStake(UInt160 staker, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetStakeCost);
            var stakeValue = _userToStake.GetValue(staker.ToBytes());
            if (stakeValue.Length == 0) 
                frame.ReturnValue = UInt256Utils.Zero.ToBytes();
            else 
                frame.ReturnValue = stakeValue;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetStartCycle)]
        public ExecutionStatus GetStartCycle(UInt160 staker, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetStartCycleCost);
            var startCycleBytes = _userToStartCycle.GetValue(staker.ToBytes());
            frame.ReturnValue = startCycleBytes;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetWithdrawRequestCycle)]
        public ExecutionStatus GetWithdrawRequestCycle(UInt160 staker, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetWithdrawRequestCycleCost);
            var withdrawRequestCycleBytes = _userToWithdrawRequestCycle.GetValue(staker.ToBytes());
            frame.ReturnValue = withdrawRequestCycleBytes;
            return ExecutionStatus.Ok;
        }
         
        private void SetStakerPublicKey(UInt160 staker, byte[] publicKey)
        {
            var key = staker.ToBytes();
            if (publicKey.Length == 0)
            {
                _userToPubKey.Delete(key);
                return;
            }
            _userToPubKey.SetValue(key, publicKey);
        }
        
        private void SetWithdrawRequestCycle(UInt160 staker, int cycle)
        {
            var key = staker.ToBytes();
            if (cycle == 0)
            {
                _userToWithdrawRequestCycle.Delete(key);
                return;
            }
            var value = BitConverter.GetBytes(cycle);
            _userToWithdrawRequestCycle.SetValue(key, value);
        }
        
        private void SetStakerId(UInt160 staker, int id)
        {
            var key = staker.ToBytes();
            if (id == 0)
            {
                _userToStakerId.Delete(key);
                return;
            }
            var value = BitConverter.GetBytes(id);
            _userToStakerId.SetValue(key, value);
        }

        public int GetStakerId(UInt160 staker)
        {
            var stakerIdBytes = _userToStakerId.GetValue(staker.ToBytes());
            if (stakerIdBytes.Length == 0) return 0;
            return BitConverter.ToInt32(stakerIdBytes);
        }
        
        private void SetStartCycle(UInt160 staker, int cycle)
        {
            var key = staker.ToBytes();
            var value = BitConverter.GetBytes(cycle);
            _userToStartCycle.SetValue(key, value);
        }
        
        private void DeleteStartCycle(UInt160 staker)
        {
            var key = staker.ToBytes();
            _userToStartCycle.Delete(key);
        }

        private bool IsPublicKeyOwner(byte[] publicKey, UInt160 expectedOwner)
        {
            var address = SystemContractUtils.PublicKeyToAddress(publicKey);
            return expectedOwner.Equals(address);
        }

        private void DeleteStaker(UInt160 staker)
        {
            SetWithdrawRequestCycle(staker, 0);
            SetStake(staker, UInt256Utils.Zero);
            SetStakerPublicKey(staker, new byte[]{});
            DeleteStartCycle(staker);
            SetStakerId(staker, 0);
        }
        
        private int AddStaker(byte[] publicKey)
        {
            var stakers = _stakers.Get();
            var id = stakers.Length  / CryptoUtils.PublicKeyLength;
            _stakers.Set(stakers.Concat(publicKey).ToArray());
            return id;
        }

        private void TryInitStoarge()
        {
            if (_stakers.Get().Length == 0)
            {
                AddStaker(new String('f', CryptoUtils.PublicKeyLength * 2).HexToBytes());
            }
            if (_vrfSeed.Get().Length == 0)
            {
                _vrfSeed.Set(Encoding.ASCII.GetBytes("test")); // initial seed
            }
        }

        private BigInteger GetNextVrfSeedNum()
        {
            var nextVrfSeed = GetNextVrfSeed();
            if (nextVrfSeed.Length == 0) return 0;
            return nextVrfSeed.ToUInt256().ToBigInteger();
        }

        private UInt160 MsgSender()
        {
            return _context.Sender ?? throw new InvalidOperationException();
        }
        
        /**
           * @dev Returns the kth value of the ordered array
           * See: http://www.cs.yale.edu/homes/aspnes/pinewiki/QuickSelect.html
           * @param _a The list of elements to pull from
           * @param _k The index, 1 based, of the elements you want to pull from when ordered
        */
        private int QuickSelect(int[] a, int k)
        {
            var aLen = a.Length;
            var a1 = new int[aLen];
            var a2 = new int[aLen];
            if (aLen == 1) return a[0];

            while (true) {
                var pivot = a[aLen / 2];
                var a1Len = 0;
                var a2Len = 0;
                int i;
                for (i = 0; i < aLen; i++) {
                    if (a[i] < pivot) {
                        a1[a1Len] = a[i];
                        a1Len++;
                    } else if (a[i] > pivot) {
                        a2[a2Len] = a[i];
                        a2Len++;
                    }
                }
                if (k <= a1Len) {
                    aLen = a1Len;
                    (a, a1) = Swap(a, a1);
                } else if (k > (aLen - a2Len)) {
                    k -= (aLen - a2Len);
                    aLen = a2Len;
                    (a, a2) = Swap(a, a2);
                } else {
                    return pivot;
                }
            }
        }

        private static (int[], int[]) Swap(int[] a, int[] b)
        {
            return (b, a);
        }
    }
}