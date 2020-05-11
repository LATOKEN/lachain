using System;
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

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class StakingContract : ISystemContract
    {
        
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private readonly ContractContext _contractContext;
        private const ulong CycleDuration = 1000; // in blocks
        private const ulong SubmissionPhaseDuration = 500; // in blocks
        private const ulong KeyGenPhaseDuration = 100; // in blocks
        private const int PublicKeyLength = 33;
        private readonly BigInteger _tokenUnitsInRoll = BigInteger.Pow(10, 21);
        private readonly StorageVariable _currentCycle; // int
        private readonly StorageVariable _nextValidators; // array of public keys
        private readonly StorageVariable _stakers; // array of public keys
        private readonly StorageVariable _vrfSeed;
        private readonly StorageVariable _nextVrfSeed;
        private readonly byte[] _role = Encoding.ASCII.GetBytes("staker");
        private readonly BigInteger _expectedValidatorsCount = 22;
        private readonly StorageMapping _userToStakerId;
        private readonly StorageMapping _userToPubKey;
        private readonly StorageMapping _userToStake;
        private readonly StorageMapping _userToStartCycle;
        private readonly StorageMapping _userToWithdrawRequestCycle;

        private static readonly ILogger<StakingContract> Logger =
            LoggerFactory.GetLoggerForClass<StakingContract>();

        public StakingContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _currentCycle = new StorageVariable(
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
            _nextVrfSeed = new StorageVariable(
                ContractRegisterer.StakingContract,
                contractContext.Snapshot.Storage,
                new BigInteger(9).ToUInt256()
            );
            TryInitStoarge();
        }

        public ContractStandard ContractStandard => ContractStandard.StakingContract;

        [ContractMethod(StakingInterface.MethodBecomeStaker)]
        public void BecomeStaker(byte[] publicKey, UInt256 amount)
        {
            EnsurePublicKeyOwner(publicKey, MsgSender());
            EnsurePositive(amount);

            // TODO: allow adding to existing stake
            var stake = GetStake(MsgSender());
            if (!stake.Equals(UInt256Utils.Zero))
            {
                throw new Exception("Already staker");
            }

            var latoken = new NativeTokenContract(_contractContext);
            
            var balance = latoken.BalanceOf(MsgSender()) ?? UInt256Utils.Zero;
            if (balance.ToMoney(true).CompareTo(amount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }
            
            var ok = latoken.Transfer(ContractRegisterer.StakingContract, amount);
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
            SetPublicKey(MsgSender(), publicKey);
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
            
            var ok = latoken.Transfer(user, stake);
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

            var ok = Vrf.IsWinner(
                publicKey,
                proof,
                GetVrfSeed(),
                _role,
                _expectedValidatorsCount,
                GetStake(MsgSender()).ToBigInteger(true) / _tokenUnitsInRoll,
                GetTotalActiveStake().ToBigInteger(true) / _tokenUnitsInRoll
            );

            if (!ok)
            {
                throw new Exception("Invalid vrf");
            }
            SetNextValidator(publicKey);
            TrySetNextVrfSeed(Vrf.ProofToHash(proof));
        }

        [ContractMethod(StakingInterface.MethodFinishCycle)]
        public void FinishCycle()
        {
            var nextValidators = _nextValidators.Get();
            if (nextValidators.Length == 0)
            {
                throw new Exception("Empty validator set");
            }
            EnsureFinishPhase();
            _vrfSeed.Set(GetNextVrfSeed());
            _nextVrfSeed.Set(new byte[]{});
            _nextValidators.Set(new byte[]{});
            SetCurrentCycle(GetCurrentCycle() + 1);
            ApplyValidatorSet(nextValidators);
        }

        private void ApplyValidatorSet(byte[] validatorSet)
        {
            byte[][] validators = {};
            for (var startByte = 0; startByte < validatorSet.Length; startByte += PublicKeyLength)
            {
                var validatorPublicKey = validatorSet.Skip(startByte).Take(PublicKeyLength).ToArray();
                validators = validators.Concat(new[] {validatorPublicKey}).ToArray();
            }
         
            var governance = new GovernanceContract(_contractContext);   
            governance.ChangeValidators(validators);
        }

        [ContractMethod(StakingInterface.MethodGetCurrentCycle)]
        public int GetCurrentCycle()
        {
            var cycle = _currentCycle.Get();
            return cycle.Length == 0 ? 0 : BitConverter.ToInt32(cycle);
        }

        private void SetCurrentCycle(int cycle)
        {
            _currentCycle.Set(BitConverter.GetBytes(cycle));
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
        public Boolean IsNextValidator(byte[] publicKey)
        {
            var validators = _nextValidators.Get();
            for (var startByte = 0; startByte < validators.Length; startByte += PublicKeyLength)
            {
                var validator = validators.Skip(startByte).Take(PublicKeyLength).ToArray();
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
            for (var startByte = PublicKeyLength; startByte < stakers.Length; startByte += PublicKeyLength)
            {
                var stakerPublicKey = stakers.Skip(startByte).Take(PublicKeyLength).ToArray();
                var stakerAddress = PublicKeyToAddress(stakerPublicKey);
                if (GetStartCycle(stakerAddress) >= GetCurrentCycle()
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
            if (stake.ToMoney(true).CompareTo(UInt256Utils.Zero.ToMoney()) != 1)
            {
                return false;
            }
            
            return GetCurrentCycle() >= GetStartCycle(staker) && GetWithdrawRequestCycle(staker) == 0;
        }
        
        private void SetStake(UInt160 staker, UInt256 amount)
        {
            var key = staker.ToBytes();
            if (amount.ToMoney(true).CompareTo(UInt256Utils.Zero.ToMoney()) == 0)
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
         
        private void SetPublicKey(UInt160 staker, byte[] publicKey)
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
            SetPublicKey(staker, new byte[]{});
            DeleteStartCycle(staker);
            SetStakerId(staker, 0);
        }
        
        private int AddStaker(byte[] publicKey)
        {
            var stakers = _stakers.Get();
            var id = stakers.Length  / PublicKeyLength;
            _stakers.Set(stakers.Concat(publicKey).ToArray());
            return id;
        }

        private void TryInitStoarge()
        {
            if (_stakers.Get().Length == 0)
            {
                AddStaker(new String('f', PublicKeyLength * 2).HexToBytes());
            }
            if (_vrfSeed.Get().Length == 0)
            {
                _vrfSeed.Set(Encoding.ASCII.GetBytes("test")); // initial seed
            }
        }
        
        private byte[] getStakerPublicKeyById(int id)
        {
            var stakers = _stakers.Get();
            var toSkip = id  * PublicKeyLength;
            return stakers.Skip(toSkip).Take(PublicKeyLength).ToArray();
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

        private void EnsureFinishPhase()
        {
            var blockNumber = _contractContext.Receipt.Block;
            var blockInCycle = blockNumber % CycleDuration;
            if (blockInCycle < SubmissionPhaseDuration || blockInCycle >= CycleDuration - KeyGenPhaseDuration)
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
    }
}