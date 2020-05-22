using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.VRF;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.ValidatorStatus
{
    public class ValidatorStatusManager : IValidatorStatusManager
    {
        private static readonly ILogger<ValidatorStatusManager> Logger = LoggerFactory.GetLoggerForClass<ValidatorStatusManager>();
        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IStateManager _stateManager;
        private bool _withdrawTriggered;
        private readonly IPrivateWallet _privateWallet;
        private bool _started;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ITransactionBuilder _transactionBuilder;
        private UInt256? _sendingTxHash;
        
        public ValidatorStatusManager(
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ITransactionBuilder transactionBuilder,
            IBlockchainContext blockchainContext,
            IPrivateWallet privateWallet,
            IStateManager stateManager,
            IValidatorAttendanceRepository validatorAttendanceRepository
        )
        {
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _transactionBuilder = transactionBuilder;
            _blockchainContext = blockchainContext;
            _privateWallet = privateWallet;
            _stateManager = stateManager;
            _validatorAttendanceRepository = validatorAttendanceRepository;
            _withdrawTriggered = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(bool isWithdrawTriggered)
        {
            if (_started) {
                Logger.LogWarning("Service already started");
                return;
            }
            
            _started = true;
            _withdrawTriggered = isWithdrawTriggered;
            new Thread(Run).Start();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Run()
        {
            try
            {
                const ulong checkInterval = 1000;
                var lastCheckedBlockHeight = (ulong) 0;
                var passingCycle = -1;
                Logger.LogInformation($"Validator status manager started");

                while (!_withdrawTriggered)
                {
                    if (lastCheckedBlockHeight == _blockchainContext.CurrentBlockHeight || GetCurrentCycle() == passingCycle)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                        continue;
                    }
                    
                    lastCheckedBlockHeight = _blockchainContext.CurrentBlockHeight;
                    
                    if (_sendingTxHash != null)
                    {
                        if (_stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(_sendingTxHash) == null)
                        {
                            Logger.LogInformation($"Transaction submitted, waiting for including in block");
                            Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                            continue;
                        }

                        _sendingTxHash = null;
                    }

                    var stake = GetStake().ToBigInteger(true);
                    var isStaker = !stake.IsZero;

                    if (!isStaker)
                    {
                    
                        var coverFeesAmount = new BigInteger(10) * BigInteger.Pow(10, 18);
                        Logger.LogInformation($"Trying to become staker");
                        var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(NodeAddress());
                        var isEnoughBalance = balance.ToWei() > StakingContract.TokenUnitsInRoll + coverFeesAmount;
                        if (isEnoughBalance)
                        {
                            var rolls = (balance.ToWei() - coverFeesAmount) / StakingContract.TokenUnitsInRoll;
                            Logger.LogInformation($"Sending transaction to become staker");
                            BecomeStaker(rolls * StakingContract.TokenUnitsInRoll);
                            continue;
                        }
                        
                        Logger.LogInformation($"Not enough balance to become staker");
                        continue;
                    }
                    
                    var requestCycle = GetWithdrawRequestCycle();
                    if (requestCycle != 0)
                    {
                        Logger.LogInformation($"Stake withdrawal triggered externally. Processing withdrawal...");
                        _withdrawTriggered = true;
                        continue;
                    }

                    if (IsDetectionPhase() && IsPreviousValidator() && !IsCheckedIn())
                    {

                        Logger.LogInformation($"The is previous validator. Trying to submit attendance detection.");
                        SubmitAttendanceDetection();
                        continue;
                    }

                    var isNextValidator = IsNextValidator();
                    if (isNextValidator)
                    {
                        Logger.LogInformation($"The node is chosen as next validator. Nothing to do.");
                        passingCycle = GetCurrentCycle();
                        continue;
                    }

                    if (!IsAbleToBeAValidator() || !IsVrfSubmissionPhase())
                    {
                        Logger.LogInformation($"Current submission phase missed. Waiting for the next one.");
                        passingCycle = GetCurrentCycle();
                        continue;
                    }

                    var (isWinner, proof) = GetVrfProof(stake);
                    if (isWinner)
                    {
                        Logger.LogInformation($"The node won the VRF lottery. Submitting transaction to become next cycle' validator");
                        SubmitVrf(proof);
                        continue;
                    }
                    Logger.LogInformation($"The node didn't win the VRF lottery. Waiting for the next cycle.");
                }
                
                lastCheckedBlockHeight = 0;
                passingCycle = 0;
                
                // Try to withdraw stake
                while (!GetStake().IsZero())
                {
                    if (lastCheckedBlockHeight == _blockchainContext.CurrentBlockHeight || GetCurrentCycle() == passingCycle)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                        continue;
                    }
                    Logger.LogWarning($"Trying to withdraw stake");
                    
                    lastCheckedBlockHeight = _blockchainContext.CurrentBlockHeight;

                    if (IsDetectionPhase() && IsPreviousValidator() && !IsCheckedIn())
                    {
                        Logger.LogInformation($"The is previous validator. Trying to submit attendance detection.");
                        SubmitAttendanceDetection();
                        continue;
                    }

                    var requestCycle = GetWithdrawRequestCycle();
                    if (requestCycle == 0)
                    {
                        if (IsNextValidator())
                        {
                            Logger.LogWarning($"Stake reserved for the next cycle. Waiting for the next cycle.");
                            passingCycle = GetCurrentCycle();
                            continue;
                        }
                        RequestStakeWithdrawal();
                        passingCycle = GetCurrentCycle();
                        Logger.LogWarning($"Submitted withdrawal stake request. Waiting for the next cycle.");
                        continue;
                    }

                    if (GetCurrentCycle() > requestCycle && IsWithdrawalPhase())
                    {
                        WithdrawStakeTx();
                        Logger.LogWarning($"Submitted stake withdrawal transaction. Waiting for the next block to ensure withdrawal succeeded.");
                    }
                }

                _started = false;
                Logger.LogWarning($"Stake withdrawn. Validator status manager stopped.");
            }
            catch (Exception e)
            {
                Logger.LogCritical($"Fatal error in validator status manager, exiting: {e}");
                Environment.Exit(1);
            }
        }

        private void BecomeStaker(BigInteger stakeAmount)
        {
                var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                    _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                    ContractRegisterer.StakingContract,
                    Money.Zero,
                    StakingInterface.MethodBecomeStaker,
                    100,
                    _privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray(),
                    (object) stakeAmount.ToUInt256()
                );
                
                AddTxToPool(tx);
        }

        private void SubmitVrf(byte[] proof)
        {
                var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                    _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                    ContractRegisterer.StakingContract,
                    Money.Zero,
                    StakingInterface.MethodSubmitVrf,
                    100,
                    _privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray(),
                    (object) proof
                );
                
                AddTxToPool(tx);
        }

        private void SubmitAttendanceDetection()
        {
            var previousValidators = GetPreviousValidators();
            
            var publicKeys = new byte[previousValidators.Length][]; 
            var attendances = new UInt256[previousValidators.Length]; 
            var attendanceData = GetValidatorAttendance();
            
            if (attendanceData == null)
            {
                Logger.LogWarning("Attendance detection data didn't collected");
                return;
            }

            for (var i = 0; i < previousValidators.Length; i++)
            {
                var publicKey = previousValidators[i];
                attendances[i] = new BigInteger(attendanceData.GetAttendance(publicKey)).ToUInt256();
                publicKeys[i] = publicKey;
            }
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodSubmitAttendanceDetection,
                100,
                publicKeys,
                attendances
            );
            attendanceData.NextCycle();
            _validatorAttendanceRepository.SaveState(attendanceData.ToBytes());
            AddTxToPool(tx);
        }

        private void AddTxToPool(Transaction tx)
        {
            var receipt = _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
            _sendingTxHash = tx.FullHash(receipt.Signature);
            _transactionPool.Add(receipt);
        }

        private void RequestStakeWithdrawal()
        {
                var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                    _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                    ContractRegisterer.StakingContract,
                    Money.Zero,
                    StakingInterface.MethodRequestStakeWithdrawal,
                    100,
                    _privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray()
                );
                
                AddTxToPool(tx);
        }

        private void WithdrawStakeTx()
        {
                var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                    _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                    ContractRegisterer.StakingContract,
                    Money.Zero,
                    StakingInterface.MethodWithdrawStake,
                    100,
                    _privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray()
                );
                
                AddTxToPool(tx);
        }

        private UInt256 GetStake()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            return stakingProtocol.GetStake(context.Sender);
        }

        private UInt256 GetTotalStake()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            return stakingProtocol.GetTotalActiveStake();
        }

        private (bool, byte[]) GetVrfProof(BigInteger stake)
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            var seed = stakingProtocol.GetVrfSeed();
            
            var rolls = stake / StakingContract.TokenUnitsInRoll;
            var totalRolls = GetTotalStake().ToBigInteger(true) / StakingContract.TokenUnitsInRoll;
            var (proof, value, j) = Vrf.Evaluate(_privateWallet.EcdsaKeyPair.PrivateKey.Buffer.ToByteArray(), seed,
                StakingContract.Role, StakingContract.ExpectedValidatorsCount, rolls, totalRolls);
            return (j > 0, proof);
        }

        private int GetWithdrawRequestCycle()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            return stakingProtocol.GetWithdrawRequestCycle(context.Sender);
        }

        private bool IsNextValidator()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            if (IsVrfSubmissionPhase())
            {
                var stakingProtocol = new StakingContract(context);
                return stakingProtocol.IsNextValidator(_privateWallet.EcdsaKeyPair.PublicKey.Buffer.ToByteArray());
            }

            var governanceProtocol = new GovernanceContract(context);
            return governanceProtocol.IsNextValidator(_privateWallet.EcdsaKeyPair.PublicKey.Buffer
                .ToByteArray());

        }

        private bool IsPreviousValidator()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };
            var staking = new StakingContract(context);
            return staking.IsPreviousValidator(_privateWallet.EcdsaKeyPair.PublicKey.Buffer
                .ToByteArray());

        }

        private byte[][] GetPreviousValidators()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };
            var staking = new StakingContract(context);
            return staking.GetPreviousValidators();
        }

        private bool IsCheckedIn()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };
            var staking = new StakingContract(context);
            return staking.IsCheckedInFaultDetector(_privateWallet.EcdsaKeyPair.PublicKey.Buffer
                .ToByteArray());

        }

        private bool IsVrfSubmissionPhase()
        {
            var blockNumber = _blockchainContext.CurrentBlockHeight;
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle < StakingContract.SubmissionPhaseDuration;
        }

        private bool IsDetectionPhase()
        {
            var blockNumber = _blockchainContext.CurrentBlockHeight;
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle < StakingContract.AttendanceDetectionDuration;
        }

        private bool IsWithdrawalPhase()
        {
            var blockNumber = _blockchainContext.CurrentBlockHeight;
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle >= StakingContract.AttendanceDetectionDuration;
        }

        private int GetCurrentCycle()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            return stakingProtocol.GetCurrentCycle();
        }

        private bool IsAbleToBeAValidator()
        {
            var context = new ContractContext
            {
                Snapshot = _stateManager.LastApprovedSnapshot,
                Sender = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
            };

            var stakingProtocol = new StakingContract(context);
            return stakingProtocol.IsAbleToBeAValidator(_privateWallet.EcdsaKeyPair.PublicKey.GetAddress());
        }

        private UInt160 NodeAddress()
        {
            return _privateWallet.EcdsaKeyPair.PublicKey.GetAddress();
        }

        public void WithdrawStakeAndStop()
        {
            _withdrawTriggered = true;
        }
        
        public bool IsStarted()
        {
            return _started;
        }

        private ValidatorAttendance? GetValidatorAttendance()
        {
            var bytes = _validatorAttendanceRepository.LoadState();
            if (bytes is null || bytes.Length == 0) return null;
            return ValidatorAttendance.FromBytes(bytes, _blockchainContext.CurrentBlockHeight / StakingContract.CycleDuration);
        }
    }
}