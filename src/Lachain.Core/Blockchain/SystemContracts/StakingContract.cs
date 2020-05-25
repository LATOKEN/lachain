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
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Crypto.VRF;
using Lachain.Utility;
using Lachain.Utility.Serialization;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class StakingContract : ISystemContract
    {
        
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private readonly ContractContext _contractContext;
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

        public StakingContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
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
        public void BecomeStaker(byte[] publicKey, UInt256 amount)
        {
            EnsurePublicKeyOwner(publicKey, MsgSender());
            
            if (amount.ToBigInteger(true) < TokenUnitsInRoll)
                throw new Exception("Amount is less than 1 roll");

            // TODO: allow adding to existing stake
            var stake = GetStake(MsgSender());
            if (!stake.IsZero())
            {
                throw new Exception("Already staker");
            }

            var laToken = new NativeTokenContract(_contractContext);
            
            var balance = laToken.BalanceOf(MsgSender()) ?? UInt256Utils.Zero;
            if (balance.ToMoney(true).CompareTo(amount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }
            
            var ok = laToken.Transfer(ContractRegisterer.StakingContract, amount.ToBytes().ToUInt256());
            if (!ok)
            {
                throw new Exception("Transfer failure");
            }

            var startingCycle = GetCurrentCycle() + 1;
            
            // Special case for initial cycle
            if (startingCycle == 1)
            {
                startingCycle--;
            }

            SetStake(MsgSender(), amount);
            SetStakerPublicKey(MsgSender(), publicKey);
            SetStartCycle(MsgSender(), startingCycle);
            var id = AddStaker(publicKey);
            SetStakerId(MsgSender(), id);
        }

        [ContractMethod(StakingInterface.MethodRequestStakeWithdrawal)]
        public void RequestStakeWithdrawal(byte[] publicKey)
        {
            EnsurePublicKeyOwner(publicKey, MsgSender());
            if (IsNextValidator(publicKey))
            {
                throw new Exception("Stake reserved for the next cycle");
            }

            var stake = GetStake(MsgSender());
            EnsurePositive(stake);

            if (GetWithdrawRequestCycle(MsgSender()) != 0)
            {
                throw new Exception("Already requested");
            }
            
            SetWithdrawRequestCycle(MsgSender(), GetCurrentCycle());
        }

        [ContractMethod(StakingInterface.MethodWithdrawStake)]
        public void WithdrawStake(byte[] publicKey)
        {
            EnsurePublicKeyOwner(publicKey, MsgSender());
            EnsureWithdrawalPhase();
            
            if (GetWithdrawRequestCycle(MsgSender()) == 0)
            {
                throw new Exception("Request not found");
            }
            
            if (GetWithdrawRequestCycle(MsgSender()) == GetCurrentCycle())
            {
                throw new Exception("Wait for the next cycle");
            }

            var stake = GetStake(MsgSender());
            EnsurePositive(stake);

            // save the real sender
            var user = MsgSender();
            // change the sender of the transaction to perform money transfer from this contract 
            _contractContext.Sender = ContractRegisterer.StakingContract;
            var latoken = new NativeTokenContract(_contractContext);
            
            var ok = latoken.Transfer(user, stake.ToBytes().ToUInt256());
            if (!ok) throw new Exception("Transfer failure");

            DeleteStaker(user);
        }

        [ContractMethod(StakingInterface.MethodSubmitVrf)]
        public void SubmitVrf(byte[] publicKey, byte[] proof)
        {
            EnsureSubmissionPhase();
            EnsurePublicKeyOwner(publicKey, MsgSender());
            if (IsNextValidator(publicKey))
            {
                throw new Exception("Already submitted");
            }

            if (!IsAbleToBeAValidator(MsgSender()))
            {
                throw new Exception("Cannot be a validator");
            }

            var totalRolls = GetTotalActiveStake().ToBigInteger(true) / TokenUnitsInRoll;
            var ok = Vrf.IsWinner(
                publicKey,
                proof,
                GetVrfSeed(),
                Role,
                ExpectedValidatorsCount,
                GetStake(MsgSender()).ToBigInteger(true) / TokenUnitsInRoll,
                totalRolls
            );

            if (!ok)
            {
                throw new Exception("Invalid vrf proof");
            }
            SetNextValidator(publicKey);
            TrySetNextVrfSeed(Vrf.ProofToHash(proof));
        }

        [ContractMethod(StakingInterface.MethodSubmitAttendanceDetection)]
        public void SubmitAttendanceDetection(byte[][] faultPersons, UInt256[] faultBlocksCounts)
        {
            EnsureDetectorPhase();
            var senderPublicKey = GetStakerPublicKey(MsgSender());
            EnsurePreviousValidator(senderPublicKey);

            if (faultPersons.Length != faultBlocksCounts.Length)
            {
                throw new Exception("Arguments length mismatch");
            }
            AttendanceDetectorCheckIn(senderPublicKey);

            for (var i = 0; i < faultPersons.Length; i++)
            {
                EnsurePreviousValidator(faultPersons[i]);
                var detectedBlocks = faultBlocksCounts[i].ToBigInteger(true);
                if (detectedBlocks > CycleDuration)
                {
                    throw new Exception("Argument out of range");
                }
                VoteForAttendance(faultPersons[i], (int) detectedBlocks);
            }
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

        private void AttendanceDetectorCheckIn(byte[] publicKey)
        {
            if (IsCheckedInFaultDetector(publicKey))
                throw new Exception("Already checked in");
            
            _attendancetDetectorCheckIns.Set(_attendancetDetectorCheckIns.Get().Concat(publicKey).ToArray());
        }

        private void ClearAttendanceDetectorCheckIns()
        {
           _attendancetDetectorCheckIns.Set(new byte[]{});
        }

        public bool IsCheckedInFaultDetector(byte[] publicKey)
        {
            var seenValidatorsBytes = _attendancetDetectorCheckIns.Get();
            for (var startByte = 0; startByte < seenValidatorsBytes.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = seenValidatorsBytes.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                    return true;
            }

            return false;
        }

        public void DistributeRewardsAndPenalties(Money totalReward)
        {

            if (!MsgSender().Equals(ContractRegisterer.GovernanceContract))
            {
                throw new Exception("Auth failure");
            }
            
            if (_contractContext.Receipt.Block % CycleDuration != AttendanceDetectionDuration)
            {
                throw new Exception("Wrong submission");
            }
            
            var validatorsData = _previousValidators.Get();
            var validatorsCount = validatorsData.Length / CryptoUtils.PublicKeyLength;
            var maxValidatorReward = totalReward / validatorsCount; 
            for (var i = 0; i < validatorsCount; i ++)
            {
                var validator = validatorsData.Skip(i * CryptoUtils.PublicKeyLength).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (!IsCheckedInFaultDetector(validator))
                {
                    SlashStake(validator, (int) CycleDuration);
                }
                else
                {
                    var activeBlocks = GetActiveBlocksCount(validator);
                    var validatorReward = maxValidatorReward * activeBlocks / (int) CycleDuration;
                    if (validatorReward > Money.Zero)
                    {
                        _contractContext.Snapshot.Balances.AddBalance(
                            PublicKeyToAddress(validator), validatorReward
                        );
                    }
                }
                ClearAttendanceVotes(validator);
            }

            ClearAttendanceDetectorCheckIns();
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

        private void SlashStake(byte[] validator, int faultBlocksCount)
        {
            var validatorAddress = PublicKeyToAddress(validator);
            var stake = GetStake(validatorAddress).ToMoney(true);
            
            // TODO: determine correct number
            var penalty = stake * faultBlocksCount / (int) CycleDuration / 2;
            if (penalty <= Money.Zero) return;
            
            SetStake(validatorAddress, (stake - penalty).ToUInt256(true));
            _contractContext.Snapshot.Balances.SubBalance(
                ContractRegisterer.StakingContract, penalty
            );
        }


        [ContractMethod(StakingInterface.MethodFinishVrfLottery)]
        public void FinishVrfLottery()
        {
            var nextValidators = _nextValidators.Get();
            
            if (nextValidators.Length == 0)
                throw new Exception("Empty validator set");
            
            EnsureFinishBlock();
            
            if (!MsgSender().IsZero()) 
                throw new Exception("Auth failure");
            
            _vrfSeed.Set(GetNextVrfSeed());
            _nextVrfSeed.Set(new byte[]{});
            _nextValidators.Set(new byte[]{});
            ApplyNextAndStorePreviousValidatorSet(nextValidators);
        }

        private void ApplyNextAndStorePreviousValidatorSet(byte[] validatorSet)
        {
            byte[][] validators = {};
            for (var startByte = 0; startByte < validatorSet.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validatorPublicKey = validatorSet.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validatorPublicKey}).ToArray();
            }

            var previousValidators = _contractContext.Snapshot.Validators.GetValidatorsPublicKeys().Select(pk => pk.Buffer.ToByteArray());
            SetPreviousValidators(previousValidators.ToArray());

            _contractContext.Sender = ContractRegisterer.StakingContract;
            var governance = new GovernanceContract(_contractContext);   
            governance.ChangeValidators(validators);
        }

        [ContractMethod(StakingInterface.MethodGetCurrentCycle)]
        public int GetCurrentCycle()
        {
            return (int)(_contractContext.Snapshot.Blocks.GetTotalBlockHeight() / CycleDuration);
        }

        [ContractMethod(StakingInterface.MethodGetVrfSeed)]
        public byte[] GetVrfSeed()
        {
            return _vrfSeed.Get();
        }

        [ContractMethod(StakingInterface.MethodGetNextVrfSeed)]
        public byte[] GetNextVrfSeed()
        {
            return _nextVrfSeed.Get();
        }

        [ContractMethod(StakingInterface.MethodIsNextValidator)]
        public bool IsNextValidator(byte[] publicKey)
        {
            var validators = _nextValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    return true;
                }
            }

            return false;
        }

        [ContractMethod(StakingInterface.MethodGetPreviousValidators)]
        public byte[][] GetPreviousValidators()
        {
            var validatorsData = _previousValidators.Get();
            byte[][] validators = {};
            for (var startByte = 0; startByte < validatorsData.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validatorsData.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validator}).ToArray();
            }

            return validators;
        }

        public void EnsurePreviousValidator(byte[] publicKey)
        {
            if (!IsPreviousValidator(publicKey)) 
                throw new Exception("Not a previous validator");
        }

        public bool IsPreviousValidator(byte[] publicKey)
        {
            var validators = _previousValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                if (validator.SequenceEqual(publicKey))
                {
                    return true;
                }
            }

            return false;
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
            if (currentVrfSeedNum == 0 || vrfSeed.ToUInt256().ToBigInteger(true) < currentVrfSeedNum)
            {
                _nextVrfSeed.Set(vrfSeed);
            }
        }

        [ContractMethod(StakingInterface.MethodTotalActiveStake)]
        public UInt256 GetTotalActiveStake()
        {
            Money stakeSum = UInt256Utils.Zero.ToMoney();
            var stakers = _stakers.Get();
            for (var startByte = CryptoUtils.PublicKeyLength; startByte < stakers.Length; startByte += CryptoUtils.PublicKeyLength)
            {
                var stakerPublicKey = stakers.Skip(startByte).Take(CryptoUtils.PublicKeyLength).ToArray();
                var stakerAddress = PublicKeyToAddress(stakerPublicKey);
                if (GetCurrentCycle() >= GetStartCycle(stakerAddress)
                    && (GetWithdrawRequestCycle(stakerAddress) == 0
                        || GetWithdrawRequestCycle(stakerAddress) == GetCurrentCycle()))
                {
                    stakeSum += GetStake(stakerAddress).ToMoney(true);
                }
            }

            return stakeSum.ToUInt256(true);
        }

        
        [ContractMethod(StakingInterface.MethodIsAbleToBeAValidator)]
        public bool IsAbleToBeAValidator(UInt160 staker)
        {
            var stake = GetStake(staker);
            if (stake.ToBigInteger(true) < TokenUnitsInRoll)
            {
                return false;
            }
            
            return GetCurrentCycle() >= GetStartCycle(staker) && GetWithdrawRequestCycle(staker) == 0;
        }
        
        private void SetStake(UInt160 staker, UInt256 amount)
        {
            var key = staker.ToBytes();
            if (amount.IsZero())
            {
                _userToStake.Delete(key);
                return;
            }
            var value = amount.ToBytes();
            _userToStake.SetValue(key, value);
        }

        [ContractMethod(StakingInterface.MethodGetStake)]
        public UInt256 GetStake(UInt160 staker)
        {
            var stakeValue = _userToStake.GetValue(staker.ToBytes());
            if (stakeValue.Length == 0) return UInt256Utils.Zero;
            return stakeValue.ToUInt256();
        }

        public int GetStartCycle(UInt160 staker)
        {
            var startCycleBytes = _userToStartCycle.GetValue(staker.ToBytes());
            if (startCycleBytes.Length == 0) throw new Exception("Start cycle not found");
            return BitConverter.ToInt32(startCycleBytes);
        }

        public int GetWithdrawRequestCycle(UInt160 staker)
        {
            var withdrawRequestCycleBytes = _userToWithdrawRequestCycle.GetValue(staker.ToBytes());
            if (withdrawRequestCycleBytes.Length == 0) return 0;
            return BitConverter.ToInt32(withdrawRequestCycleBytes);
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

        private void EnsurePublicKeyOwner(byte[] publicKey, UInt160 expectedOwner)
        {
            var address = PublicKeyToAddress(publicKey);
            if (!expectedOwner.Equals(address))
            {
                throw new Exception("Incorrect public key");
            }
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
        
        private byte[] GetStakerPublicKey(UInt160 staker)
        {
            var pubKey = _userToPubKey.GetValue(staker.ToBytes());
            if (pubKey.Length == 0)
            {
                throw new Exception("Not a staker");
            }

            return pubKey;
        }

        private UInt160 PublicKeyToAddress(byte[] publicKey)
        {
            return Crypto.ComputeAddress(publicKey).ToUInt160();
        }

        private static void EnsurePositive(UInt256 amount)
        {
            if (amount.ToMoney(true).CompareTo(UInt256Utils.ToUInt256(0).ToMoney()) != 1)
            {
                throw new Exception("Should be positive");
            }
        }

        private void EnsureSubmissionPhase()
        {
            var blockNumber = _contractContext.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= SubmissionPhaseDuration)
            {
                throw new Exception("Not a submission phase");
            }
        }

        private void EnsureFinishBlock()
        {
            var blockNumber = _contractContext.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle != CycleDuration / 2)
            {
                throw new Exception("Not a finish phase");
            }
        }

        private void EnsureWithdrawalPhase()
        {
            var blockNumber = _contractContext.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle < AttendanceDetectionDuration)
            {
                throw new Exception("Not a finish phase");
            }
        }

        private void EnsureDetectorPhase()
        {
            var blockNumber = _contractContext.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle >= AttendanceDetectionDuration)
            {
                throw new Exception("Not a finish phase");
            }
        }

        private BigInteger GetNextVrfSeedNum()
        {
            var nextVrfSeed = GetNextVrfSeed();
            if (nextVrfSeed.Length == 0) return 0;
            return nextVrfSeed.ToUInt256().ToBigInteger(true);
        }

        private UInt160 MsgSender()
        {
            return _contractContext.Sender ?? throw new InvalidOperationException();
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