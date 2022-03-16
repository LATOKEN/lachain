using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Networking;
using Lachain.Utility.Utils;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using LibVRF.Net;
using Nethereum.Util;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class StakingContract : ISystemContract
    {
        public static BigInteger ExpectedValidatorsCount = 4;
        public static ulong CycleDuration = 20; // in blocks
        public static ulong VrfSubmissionPhaseDuration = CycleDuration / 2; // in blocks
        public static ulong AttendanceDetectionDuration = CycleDuration / 10; // in blocks
        public static bool AlreadySet { get; private set; }


        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private readonly InvocationContext _context;
        public static readonly BigInteger TokenUnitsInRoll = BigInteger.Pow(10, 21);
        private readonly StorageVariable _nextValidatorPubKeys; // array of public keys
        private readonly StorageVariable _previousValidatorPubKeys; // array of public keys
        private readonly StorageVariable _attendanceDetectorCheckInPubKeys; // array of public keys
        private readonly StorageVariable _stakedPubKeys; // array of public keys
        private readonly StorageVariable _vrfSeed;
        private readonly StorageVariable _nextVrfSeed;
        public static readonly byte[] Role = Encoding.ASCII.GetBytes("staker");
        private readonly StorageMapping _stakedAddressToStakedPubKey;
        private readonly StorageMapping _stakedAddressToStake;
        private readonly StorageMapping _stakedAddressToPenalty;
        private readonly StorageMapping _stakedAddressToStartCycle;
        private readonly StorageMapping _stakedAddressToWithdrawRequestCycle;
        private readonly StorageMapping _attendanceVotes;
        private readonly StorageMapping _stakedPubKeyToStakerAddress; // maps public key of the validator to staker address


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
            _stakedAddressToPenalty = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(1).ToUInt256()
            );
            _stakedAddressToStakedPubKey = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(2).ToUInt256()
            );
            _stakedAddressToStake = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(3).ToUInt256()
            );
            _stakedAddressToStartCycle = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(4).ToUInt256()
            );
            _stakedAddressToWithdrawRequestCycle = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(5).ToUInt256()
            );
            _stakedPubKeys = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(6).ToUInt256()
            );
            _vrfSeed = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(7).ToUInt256()
            );
            _nextValidatorPubKeys = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(8).ToUInt256()
            );
            _previousValidatorPubKeys = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(9).ToUInt256()
            );
            _attendanceDetectorCheckInPubKeys = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(10).ToUInt256()
            );
            _attendanceVotes = new StorageMapping(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(11).ToUInt256()
            );
            _stakedPubKeyToStakerAddress = new StorageMapping(
              ContractRegisterer.StakingContract,
              contractContext.Snapshot.Storage,
              new BigInteger(12).ToUInt256()
            );
        }

        public static void Initialize(NetworkConfig networkConfig)
        {
            if(networkConfig is null)
                throw new Exception("network config passed in staking contract is null");
            
            if(AlreadySet == true)
                throw new Exception("Staking Contract can't be initialized more than once");
            
            AlreadySet = true;
            
            if(networkConfig.CycleDuration is null)
                throw new Exception("Cycle Duration is not provided");
            CycleDuration = (ulong) networkConfig.CycleDuration;

            if(networkConfig.ValidatorsCount is null)
                throw new Exception("Validator Count is not provided");
            ExpectedValidatorsCount = new BigInteger((ulong) networkConfig.ValidatorsCount);
            VrfSubmissionPhaseDuration = CycleDuration / 2; 
            AttendanceDetectionDuration = CycleDuration / 10;

            Logger.LogTrace($"Initializing staking contract done.");
        }

        public ContractStandard ContractStandard => ContractStandard.StakingContract;

        [ContractMethod(StakingInterface.MethodBecomeStaker)]
        public ExecutionStatus BecomeStaker(byte[] stakedPubKey, UInt256 amount, SystemContractExecutionFrame frame)
        {
            Logger.LogInformation($"Executing BecomeStaker for validator: {stakedPubKey.ToHex()} and LA: {amount.ToHex()}");

            frame.UseGas(GasMetering.StakingBecomeStakerCost);

            // address should also be able to stake for other validator

            if (amount.ToBigInteger() < TokenUnitsInRoll)
                return ExecutionStatus.ExecutionHalted;

            // check if any address (this validator address itself or some other address) has already got some stake for the validator.

            var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                Hepler.PublicKeyToAddress(stakedPubKey));


            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var stake = getStakeExecutionResult.ReturnValue!.ToUInt256();
            if (!stake.IsZero())
                return ExecutionStatus.ExecutionHalted;

            // check MsgSender = Staker's balance 

            var balanceOfExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, ContractRegisterer.StakingContract, Lrc20Interface.MethodBalanceOf,
                MsgSender());

            if (balanceOfExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var balance = balanceOfExecutionResult.ReturnValue!.ToUInt256().ToMoney();

            if (balance.CompareTo(amount.ToMoney()) == -1)
                return ExecutionStatus.ExecutionHalted;

            // transfer amount from staker's address to staking contract 

            var transferExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.LatokenContract, MsgSender(), Lrc20Interface.MethodTransfer,
                ContractRegisterer.StakingContract, amount);

            if (transferExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var fail = transferExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (fail)
                return ExecutionStatus.ExecutionHalted;

            var startingCycle = GetCurrentCycle() + 1;

            // Special case for initial validator
            if (startingCycle == 1)
            {
                startingCycle--;
            }

            SetStake(Hepler.PublicKeyToAddress(stakedPubKey), amount); // store the stake amount to the address of the validator

            SetStakedPublicKey(MsgSender(), stakedPubKey);

            SetStartCycle(Hepler.PublicKeyToAddress(stakedPubKey), startingCycle);

            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodRequestStakeWithdrawal)]
        public ExecutionStatus RequestStakeWithdrawal(byte[] stakedPubKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingRequestStakeWithdrawalCost);

            // check the address trying to withdraw is indeed the address which staked before or validator itself
            var staker = GetStaker(stakedPubKey);
            if (staker == null) return ExecutionStatus.ExecutionHalted;
            if (IsPublicKeyOwner(stakedPubKey, MsgSender()) == false && staker!.Equals(MsgSender()) == false)
            {
                return ExecutionStatus.ExecutionHalted;
            }

            var isNextValidatorExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodIsNextValidator, stakedPubKey);

            if (isNextValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var isNextValidator = !isNextValidatorExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (isNextValidator)
                return ExecutionStatus.ExecutionHalted;

            // get the amount that was staked for this validator
            var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                Hepler.PublicKeyToAddress(stakedPubKey));

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var stake = getStakeExecutionResult.ReturnValue!.ToUInt256();

            if (stake.IsZero())
                return ExecutionStatus.ExecutionHalted;

            var getWithdrawRequestCycleExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodGetWithdrawRequestCycle, Hepler.PublicKeyToAddress(stakedPubKey));

            if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;

            if (withdrawRequestCycleBytes!.Length != 0)
                return ExecutionStatus.ExecutionHalted;

            SetWithdrawRequestCycle(Hepler.PublicKeyToAddress(stakedPubKey), GetCurrentCycle());

            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodWithdrawStake)]
        public ExecutionStatus WithdrawStake(byte[] stakedPubKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingWithdrawStakeCost);
            Logger.LogInformation($"Withdrawing stake");
            var staker = GetStaker(stakedPubKey);
            if (staker == null) return ExecutionStatus.ExecutionHalted;
            if (IsPublicKeyOwner(stakedPubKey, MsgSender()) == false && staker!.Equals(MsgSender()) == false)
            {
                return ExecutionStatus.ExecutionHalted;
            }
            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle < AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;

            var getWithdrawRequestCycleExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodGetWithdrawRequestCycle, Hepler.PublicKeyToAddress(stakedPubKey));

            if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;

            if (withdrawRequestCycleBytes!.Length == 0)
                return ExecutionStatus.ExecutionHalted;

            var withdrawRequestCycle = BitConverter.ToInt32(withdrawRequestCycleBytes);

            if (withdrawRequestCycle == GetCurrentCycle())
                return ExecutionStatus.ExecutionHalted;

            var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                Hepler.PublicKeyToAddress(stakedPubKey));

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var stake = getStakeExecutionResult.ReturnValue!.ToUInt256();

            if (stake.IsZero())
                return ExecutionStatus.ExecutionHalted;

            var getPenaltyExecutionResult = Hepler.CallSystemContract(
                frame,
                ContractRegisterer.StakingContract, 
                ContractRegisterer.StakingContract, StakingInterface.MethodGetPenalty,
                Hepler.PublicKeyToAddress(stakedPubKey)
            );

            if (getPenaltyExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var penalty = getPenaltyExecutionResult.ReturnValue!.ToUInt256();

            if (stake.ToMoney().CompareTo(penalty.ToMoney()) < 0)
            {
                penalty = stake;
                stake = UInt256Utils.Zero;
            }
            else
            {
                stake = (stake.ToMoney() - penalty.ToMoney()).ToUInt256();
            }

            // give back the tokens to the staker 
            var transferStakeExecutionResult = Hepler.CallSystemContract(
                frame,
                ContractRegisterer.LatokenContract, 
                ContractRegisterer.StakingContract, 
                Lrc20Interface.MethodTransfer,
                GetStaker(stakedPubKey)!, 
                stake
            );

            if (transferStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var fail = transferStakeExecutionResult.ReturnValue!.ToUInt256().IsZero();
            Logger.LogInformation($"transferStakeExecutionResult {fail}");
            if (fail)
                return ExecutionStatus.ExecutionHalted;

            var burnPenaltyExecutionResult = Hepler.CallSystemContract(
                frame,
                ContractRegisterer.LatokenContract, 
                ContractRegisterer.StakingContract, 
                Lrc20Interface.MethodTransfer,
                UInt160Utils.Zero, 
                penalty
            );
            
            if (burnPenaltyExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            fail = burnPenaltyExecutionResult.ReturnValue!.ToUInt256().IsZero();
            Logger.LogInformation($"burnPenaltyExecutionResult {fail}");
            if (fail)
                return ExecutionStatus.ExecutionHalted;

            DeleteStaked(Hepler.PublicKeyToAddress(stakedPubKey));
            Logger.LogInformation($"Staker removed");
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodSubmitVrf)]
        public ExecutionStatus SubmitVrf(byte[] publicKey, byte[] proof, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingSubmitVrfCost);

            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= VrfSubmissionPhaseDuration)
                return ExecutionStatus.ExecutionHalted;

            var ok = IsPublicKeyOwner(publicKey, MsgSender());
            if (!ok) return ExecutionStatus.ExecutionHalted;

            var isNextValidatorExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodIsNextValidator, publicKey);

            if (isNextValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var isNextValidator = !isNextValidatorExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (isNextValidator)
                return ExecutionStatus.ExecutionHalted;

            var isAbleToBeValidatorExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, MsgSender(), StakingInterface.MethodIsAbleToBeValidator,
                MsgSender());

            if (isAbleToBeValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var isAbleToBeValidator = !isAbleToBeValidatorExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (!isAbleToBeValidator)
                return ExecutionStatus.ExecutionHalted;

            var getTotalStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodGetTotalActiveStake);

            if (getTotalStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var totalStake = getTotalStakeExecutionResult.ReturnValue!.ToUInt256().ToBigInteger();
            var totalRolls = totalStake / TokenUnitsInRoll;

            var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                MsgSender());

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var stake = getStakeExecutionResult.ReturnValue!.ToUInt256().ToBigInteger();

            var getVrfSeedExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodGetVrfSeed);

            if (getVrfSeedExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var vrfSeed = getVrfSeedExecutionResult.ReturnValue!;
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
        public ExecutionStatus SubmitAttendanceDetection(byte[][] faultPersons, UInt256[] faultBlocksCounts,
            SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingSubmitAttendanceDetectionCost);

            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;

            if (faultPersons.Length != faultBlocksCounts.Length)
                return ExecutionStatus.ExecutionHalted;


            var senderPublicKey = _stakedAddressToStakedPubKey.GetValue(MsgSender().ToBytes());
            if (senderPublicKey.Length == 0)
                return ExecutionStatus.ExecutionHalted;

            var isSenderPreviousValidatorExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodIsPreviousValidator, senderPublicKey);

            if (isSenderPreviousValidatorExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var isSenderPreviousValidator = !isSenderPreviousValidatorExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (!isSenderPreviousValidator)
                return ExecutionStatus.ExecutionHalted;

            var isCheckedInAttendanceDetectionExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                StakingInterface.MethodIsCheckedInAttendanceDetection, senderPublicKey);

            if (isCheckedInAttendanceDetectionExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var isCheckedInAttendanceDetection =
                !isCheckedInAttendanceDetectionExecutionResult.ReturnValue!.ToUInt256().IsZero();
            if (isCheckedInAttendanceDetection)
                return ExecutionStatus.ExecutionHalted;

            _attendanceDetectorCheckInPubKeys.Set(_attendanceDetectorCheckInPubKeys.Get().Concat(senderPublicKey).ToArray());

            for (var i = 0; i < faultPersons.Length; i++)
            {
                var isPreviousValidatorExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodIsPreviousValidator, faultPersons[i]);

                if (isPreviousValidatorExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var isPreviousValidator = !isPreviousValidatorExecutionResult.ReturnValue!.ToUInt256().IsZero();
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
            _attendanceDetectorCheckInPubKeys.Set(new byte[] { });
        }

        [ContractMethod(StakingInterface.MethodIsCheckedInAttendanceDetection)]
        public ExecutionStatus IsCheckedInAttendanceDetection(byte[] publicKey, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingIsCheckedInAttendanceDetectionCost);

            var result = false;
            var seenValidatorsBytes = _attendanceDetectorCheckInPubKeys.Get();
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


            Logger.LogDebug($"This cycle reward {totalReward.ToMoney()}");

            if (_context.Receipt.Block % CycleDuration != AttendanceDetectionDuration)
                return ExecutionStatus.ExecutionHalted;

            var validatorsData = _previousValidatorPubKeys.Get();
            var validatorsCount = validatorsData.Length / CryptoUtils.PublicKeyLength;
            var maxValidatorReward = totalReward.ToMoney() / validatorsCount;
            for (var i = 0; i < validatorsCount; i++)
            {
                var validator = validatorsData.Skip(i * CryptoUtils.PublicKeyLength).Take(CryptoUtils.PublicKeyLength)
                    .ToArray();

                var isCheckedInAttendanceDetectionExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodIsCheckedInAttendanceDetection, validator);

                if (isCheckedInAttendanceDetectionExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var notCheckedIn = isCheckedInAttendanceDetectionExecutionResult.ReturnValue!.ToUInt256().IsZero();
                var validatorAddress = Hepler.PublicKeyToAddress(validator);

                if (notCheckedIn)
                {
                    /* Penalty for the cycle */

                    // TODO: determine correct number
                    var penalty = maxValidatorReward;
                    if (penalty > Money.Zero)
                    {
                        Logger.LogDebug($"Penalty: {penalty} LA for {validatorAddress.ToHex()}");
                        AddPenalty(validatorAddress, penalty);
                    }
                }
                /* Reward for the cycle */

                var activeBlocks = GetActiveBlocksCount(validator);
                Logger.LogDebug($"Attendance: {activeBlocks} address: {validatorAddress.ToHex()}");
                var validatorReward = maxValidatorReward * activeBlocks / (int) CycleDuration;
                Logger.LogDebug($"Total reward: {validatorReward} LA for {validatorAddress.ToHex()}");

                var rewardToMint = SubPenalty(validatorAddress, validatorReward);
                if (rewardToMint > Money.Zero)
                {
                    var newBalance = _context.Snapshot.Balances.AddBalance(
                        validatorAddress, rewardToMint, true
                    );
                    Logger.LogDebug($"Minted reward: {rewardToMint} LA for {validatorAddress.ToHex()}");
                    Logger.LogDebug($"New balance: {newBalance} LA of {validatorAddress.ToHex()}");
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
            Logger.LogTrace($"Executing finish vrf lottery");
            if (!MsgSender().IsZero())
                return ExecutionStatus.ExecutionHalted;

            var blockNumber = _context.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle != CycleDuration / 2)
                return ExecutionStatus.ExecutionHalted;

            var nextValidators = _nextValidatorPubKeys.Get();

            Logger.LogTrace($"Executing finish vrf lottery. current height: {_context.Snapshot.Blocks.GetTotalBlockHeight()}");

            if(!HardforkHeights.IsHardfork_1Active(_context.Snapshot.Blocks.GetTotalBlockHeight()))
            {
                if (nextValidators.Length == 0)
                    return ExecutionStatus.ExecutionHalted;
            }
            else 
            {
                if(nextValidators.Length <= CryptoUtils.PublicKeyLength)
                {
                    Logger.LogWarning($"Only {nextValidators.Length / CryptoUtils.PublicKeyLength} validator was chosen, so validator set is not going to change");
                    nextValidators = _context.Snapshot.Validators.GetValidatorsPublicKeys()
                    .Select(pk => pk.Buffer.ToByteArray()).Flatten().ToArray();
                }
            }

            _vrfSeed.Set(GetNextVrfSeed());
            _nextVrfSeed.Set(new byte[] { });
            _nextValidatorPubKeys.Set(new byte[] { });

            byte[][] validators = { };
            for (var startByte = 0; startByte < nextValidators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validatorPublicKey = nextValidators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validatorPublicKey}).ToArray();
            }

            var previousValidators = _context.Snapshot.Validators.GetValidatorsPublicKeys()
                .Select(pk => pk.Buffer.ToByteArray());
            SetPreviousValidators(previousValidators.ToArray());

            var changeValidatorsExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.GovernanceContract, ContractRegisterer.StakingContract,
                GovernanceInterface.MethodChangeValidators, 
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_context.Snapshot.Blocks.GetTotalBlockHeight())),
                validators);

            if (changeValidatorsExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            return ExecutionStatus.Ok;
        }

        public int GetCurrentCycle()
        {
            return (int) (_context.Snapshot.Blocks.GetTotalBlockHeight() / CycleDuration);
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
            var validators = _nextValidatorPubKeys.Get();
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
            var validatorsData = _previousValidatorPubKeys.Get();
            byte[][] validators = { };
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
            var validators = _previousValidatorPubKeys.Get();
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
            _nextValidatorPubKeys.Set(_nextValidatorPubKeys.Get().Concat(publicKey).ToArray());
        }

        private void SetPreviousValidators(byte[][] publicKeys)
        {
            _previousValidatorPubKeys.Set(publicKeys.Flatten().ToArray());
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
            var stakedPubKeys = _stakedPubKeys.Get();
            for (var startByte = CryptoUtils.PublicKeyLength;
                startByte < stakedPubKeys.Length;
                startByte += CryptoUtils.PublicKeyLength)
            {
                var stakedPublicKey = stakedPubKeys.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                var stakedAddress = Hepler.PublicKeyToAddress(stakedPublicKey);

                var getWithdrawRequestCycleExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodGetWithdrawRequestCycle, stakedAddress);

                if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;
                if (withdrawRequestCycleBytes is null)
                    return ExecutionStatus.ExecutionHalted;

                var withdrawRequestCycle = withdrawRequestCycleBytes.Length > 0
                    ? BitConverter.ToInt32(withdrawRequestCycleBytes)
                    : 0;

                var getStartCycleExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodGetWithdrawRequestCycle, stakedAddress);

                if (getStartCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var startCycleBytes = getStartCycleExecutionResult.ReturnValue;
                if (startCycleBytes is null)
                    return ExecutionStatus.ExecutionHalted;

                var startCycle = startCycleBytes.Length > 0 ? BitConverter.ToInt32(startCycleBytes) : 0;

                if (GetCurrentCycle() >= startCycle
                    && (withdrawRequestCycle == 0
                        || withdrawRequestCycle == GetCurrentCycle()))
                {
                    var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                        ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                        StakingInterface.MethodGetStake, stakedAddress);

                    if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                        return ExecutionStatus.ExecutionHalted;

                    var stake = getStakeExecutionResult.ReturnValue?.ToUInt256().ToMoney();
                    if (stake is null) return ExecutionStatus.ExecutionHalted;
                    stakeSum += stake;
                }
            }

            frame.ReturnValue = stakeSum.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }


        [ContractMethod(StakingInterface.MethodIsAbleToBeValidator)]
        public ExecutionStatus IsAbleToBeValidator(UInt160 stakedAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingIsAbleToBeValidatorCost);

            var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                ContractRegisterer.StakingContract, ContractRegisterer.StakingContract, StakingInterface.MethodGetStake,
                MsgSender());

            if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                return ExecutionStatus.ExecutionHalted;

            var stake = getStakeExecutionResult.ReturnValue?.ToUInt256();
            if (stake is null)
                return ExecutionStatus.ExecutionHalted;

            var notAStaker = stake.IsZero();
            if (notAStaker)
                frame.ReturnValue = 0.ToUInt256().ToBytes();
            else
            {
                var getWithdrawRequestCycleExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodGetWithdrawRequestCycle, stakedAddress);

                if (getWithdrawRequestCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var withdrawRequestCycleBytes = getWithdrawRequestCycleExecutionResult.ReturnValue;

                var withdrawRequestCycle = withdrawRequestCycleBytes!.Length > 0
                    ? BitConverter.ToInt32(withdrawRequestCycleBytes)
                    : 0;

                var getStartCycleExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodGetWithdrawRequestCycle, stakedAddress);

                if (getStartCycleExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var startCycleBytes = getStartCycleExecutionResult.ReturnValue;

                var startCycle = startCycleBytes!.Length > 0 ? BitConverter.ToInt32(startCycleBytes) : 0;

                frame.ReturnValue =
                    (GetCurrentCycle() >= startCycle && withdrawRequestCycle == 0 ? 1 : 0)
                    .ToUInt256().ToBytes();
            }

            return ExecutionStatus.Ok;
        }

        private void SetStake(UInt160 stakedAddress, UInt256 amount)
        {
            var key = stakedAddress.ToBytes();
            if (amount.IsZero())
            {
                _stakedAddressToStake.Delete(key);
                return;
            }

            var value = amount.ToBytes();
            _stakedAddressToStake.SetValue(key, value);
        }

        private void AddPenalty(UInt160 stakedAddress, Money amount)
        {
            var key = stakedAddress.ToBytes();
            var penaltyValue = _stakedAddressToPenalty.GetValue(stakedAddress.ToBytes());
            var previousPenalty = penaltyValue.Length == 0 ? Money.Zero : penaltyValue.ToUInt256().ToMoney();
            var newPenalty = previousPenalty + amount;
            var value = newPenalty.ToUInt256().ToBytes();
            _stakedAddressToPenalty.SetValue(key, value);
        }

        private Money SubPenalty(UInt160 stakedAddress, Money amount)
        {
            var key = stakedAddress.ToBytes();
            var penaltyValue = _stakedAddressToPenalty.GetValue(stakedAddress.ToBytes());
            var previousPenalty = penaltyValue.Length == 0 ? Money.Zero : penaltyValue.ToUInt256().ToMoney();

            // Logger.LogDebug($"Previous penalty: {previousPenalty} LA of {staker.ToHex()}");

            if (amount == Money.Zero || previousPenalty == Money.Zero)
                return amount;

            var newPenalty = previousPenalty - amount;
            if (newPenalty <= Money.Zero)
            {
                _stakedAddressToPenalty.Delete(key);
                return amount - previousPenalty;
            }

            var value = newPenalty.ToUInt256().ToBytes();
            _stakedAddressToPenalty.SetValue(key, value);
            return Money.Zero;
        }

        private void DeletePenalty(UInt160 stakedAddress)
        {
            var key = stakedAddress.ToBytes();
            _stakedAddressToPenalty.SetValue(key, UInt256Utils.Zero.ToBytes());
        }

        [ContractMethod(StakingInterface.MethodGetStake)]
        public ExecutionStatus GetStake(UInt160 stakedAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetStakeCost);
            var stakeValue = _stakedAddressToStake.GetValue(stakedAddress.ToBytes());
            frame.ReturnValue = stakeValue.Length == 0 ? UInt256Utils.Zero.ToBytes() : stakeValue;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetStakerTotalStake)]
        public ExecutionStatus  GetStakerTotalStake(UInt160 stakerAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetTotalActiveStakeCost);

            Money stakeSum = UInt256Utils.Zero.ToMoney();
            var stakerPubKeys = _stakedPubKeys.Get();
            for (var startByte = CryptoUtils.PublicKeyLength;
                startByte < stakerPubKeys.Length;
                startByte += CryptoUtils.PublicKeyLength)
            {
                var stakedPublicKey = stakerPubKeys.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                var currentStaker = GetStakerByPublicKey(stakedPublicKey);
                if (!currentStaker.Equals(stakerAddress))
                    continue;
                
                var stakedAddress = Hepler.PublicKeyToAddress(stakedPublicKey);

                var getStakeExecutionResult = Hepler.CallSystemContract(frame,
                    ContractRegisterer.StakingContract, ContractRegisterer.StakingContract,
                    StakingInterface.MethodGetStake, stakedAddress);

                if (getStakeExecutionResult.Status != ExecutionStatus.Ok)
                    return ExecutionStatus.ExecutionHalted;

                var stake = getStakeExecutionResult.ReturnValue?.ToUInt256().ToMoney();
                if (stake is null) 
                    return ExecutionStatus.ExecutionHalted;
                
                stakeSum += stake;
            }

            frame.ReturnValue = stakeSum.ToUInt256().ToBytes();
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetPenalty)]
        public ExecutionStatus GetPenalty(UInt160 stakedAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetPenaltyCost);
            var penaltyValue = _stakedAddressToPenalty.GetValue(stakedAddress.ToBytes());
            frame.ReturnValue = penaltyValue.Length == 0 ? UInt256Utils.Zero.ToBytes() : penaltyValue;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetStartCycle)]
        public ExecutionStatus GetStartCycle(UInt160 stakedAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetStartCycleCost);
            var startCycleBytes = _stakedAddressToStartCycle.GetValue(stakedAddress.ToBytes());
            frame.ReturnValue = startCycleBytes;
            return ExecutionStatus.Ok;
        }

        [ContractMethod(StakingInterface.MethodGetWithdrawRequestCycle)]
        public ExecutionStatus GetWithdrawRequestCycle(UInt160 stakedAddress, SystemContractExecutionFrame frame)
        {
            frame.UseGas(GasMetering.StakingGetWithdrawRequestCycleCost);
            var withdrawRequestCycleBytes = _stakedAddressToWithdrawRequestCycle.GetValue(stakedAddress.ToBytes());
            frame.ReturnValue = withdrawRequestCycleBytes;
            return ExecutionStatus.Ok;
        }

        private void SetStakedPublicKey(UInt160 stakerAddress, byte[] stakedPubKey)
        {
            AddStaked(stakedPubKey);
            _stakedAddressToStakedPubKey.SetValue(Hepler.PublicKeyToAddress(stakedPubKey).ToBytes(), stakedPubKey);
            _stakedPubKeyToStakerAddress.SetValue(stakedPubKey, stakerAddress.ToBytes());
        }

        private UInt160 GetStakerByPublicKey(byte[] publicKey)
        {
            return _stakedPubKeyToStakerAddress.GetValue(publicKey).ToUInt160();
        }

        private void DeleteStakedPublicKey(UInt160 stakedAddress)
        {
            var key = stakedAddress.ToBytes();
            var publicKey = _stakedAddressToStakedPubKey.GetValue(key);
            var stakedPubKeys = _stakedPubKeys.Get();
            for (var i = 0; i < stakedPubKeys.Length; i += CryptoUtils.PublicKeyLength)
            {
                var curr = stakedPubKeys.Slice(i, i + CryptoUtils.PublicKeyLength);
                if (curr.SequenceEqual(publicKey))
                    stakedPubKeys = stakedPubKeys.Take(i).Concat(stakedPubKeys.Skip(i + CryptoUtils.PublicKeyLength))
                        .ToArray();
            }

            _stakedPubKeys.Set(stakedPubKeys);
            _stakedPubKeyToStakerAddress.Delete(_stakedAddressToStakedPubKey.GetValue(key));
            _stakedAddressToStakedPubKey.Delete(key);
        }

        private void SetWithdrawRequestCycle(UInt160 stakedAddress, int cycle)
        {
            var key = stakedAddress.ToBytes();
            if (cycle == 0)
            {
                _stakedAddressToWithdrawRequestCycle.Delete(key);
                return;
            }

            var value = BitConverter.GetBytes(cycle);
            _stakedAddressToWithdrawRequestCycle.SetValue(key, value);
        }

        private void SetStartCycle(UInt160 stakedAddress, int cycle)
        {
            var key = stakedAddress.ToBytes();
            var value = BitConverter.GetBytes(cycle);
            _stakedAddressToStartCycle.SetValue(key, value);
        }

        private void DeleteStartCycle(UInt160 stakedAddress)
        {
            var key = stakedAddress.ToBytes();
            _stakedAddressToStartCycle.Delete(key);
        }

        private bool IsPublicKeyOwner(byte[] publicKey, UInt160 expectedOwner)
        {
            var address = Hepler.PublicKeyToAddress(publicKey);
            return expectedOwner.Equals(address);
        }

        private void DeleteStaked(UInt160 stakedAddress)
        {
            SetWithdrawRequestCycle(stakedAddress, 0);
            SetStake(stakedAddress, UInt256Utils.Zero);
            DeletePenalty(stakedAddress);
            DeleteStakedPublicKey(stakedAddress);
            DeleteStartCycle(stakedAddress);
        }

        private void AddStaked(byte[] stakedPubKey)
        {
            var stakedPubKeys = _stakedPubKeys.Get();
            _stakedPubKeys.Set(stakedPubKeys.Concat(stakedPubKey).ToArray());
        }

        private UInt160? GetStaker(byte[] stakedPubKey)
        {
            if (stakedPubKey == null) return null;
            byte[] stakerByte = _stakedPubKeyToStakerAddress.GetValue(stakedPubKey);
            if (stakerByte == null || stakerByte.Length == 0) return null;
            return stakerByte.ToUInt160();
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

            while (true)
            {
                var pivot = a[aLen / 2];
                var a1Len = 0;
                var a2Len = 0;
                int i;
                for (i = 0; i < aLen; i++)
                {
                    if (a[i] < pivot)
                    {
                        a1[a1Len] = a[i];
                        a1Len++;
                    }
                    else if (a[i] > pivot)
                    {
                        a2[a2Len] = a[i];
                        a2Len++;
                    }
                }

                if (k <= a1Len)
                {
                    aLen = a1Len;
                    (a, a1) = Swap(a, a1);
                }
                else if (k > (aLen - a2Len))
                {
                    k -= (aLen - a2Len);
                    aLen = a2Len;
                    (a, a2) = Swap(a, a2);
                }
                else
                {
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