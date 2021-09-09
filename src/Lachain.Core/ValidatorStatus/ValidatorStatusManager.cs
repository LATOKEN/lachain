using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Error;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using LibVRF.Net;

namespace Lachain.Core.ValidatorStatus
{
    public class ValidatorStatusManager : IValidatorStatusManager
    {
        private static readonly ILogger<ValidatorStatusManager> Logger =
            LoggerFactory.GetLoggerForClass<ValidatorStatusManager>();

        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private readonly IStateManager _stateManager;
        private bool _withdrawTriggered;
        private readonly IPrivateWallet _privateWallet;
        private bool _started;
        private Thread? _thread;
        private bool _stopRequested;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ISystemContractReader _systemContractReader;
        private UInt256? _sendingTxHash;
        private BigInteger? _stakeSize;
        private byte[] _nodePublicKey = null!;

        public ValidatorStatusManager(
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ITransactionBuilder transactionBuilder,
            IPrivateWallet privateWallet,
            IStateManager stateManager,
            IValidatorAttendanceRepository validatorAttendanceRepository,
            ISystemContractReader systemContractReader
        )
        {
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _stateManager = stateManager;
            _validatorAttendanceRepository = validatorAttendanceRepository;
            _systemContractReader = systemContractReader;
            _withdrawTriggered = false;
            _started = false;
            _thread = null;
            _stopRequested = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartWithStake(UInt256 stake, byte[] publicKey = null!)
        {
            if (_started)
            {
                Logger.LogInformation("ValidatorStatusManager already started");
                return;
            }
            
            if (publicKey == null)
            { 
                _nodePublicKey = _systemContractReader.NodePublicKey();
            }
            else
            {
                _nodePublicKey = publicKey;
            }
            
            _stakeSize = new Money(stake).ToWei();
            Start(false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(bool isWithdrawTriggered)
        {
            if (_started)
            {
                Logger.LogInformation("ValidatorStatusManager already started");
                return;
            }

            _started = true;
            _stopRequested = false;
            _withdrawTriggered = isWithdrawTriggered;
            _thread = new Thread(Run);
            _thread.Start();
        }

        public void Stop()
        {
            if (!_started)
                return;

            _stopRequested = true;
            if (_thread?.ThreadState == ThreadState.Running)
                _thread?.Join();
            _thread = null;
        }

        public void Dispose()
        {
            Stop();
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
                    if (_stopRequested)
                        break;
                    if (lastCheckedBlockHeight == _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() ||
                        GetCurrentCycle() == passingCycle)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                        continue;
                    }

                    lastCheckedBlockHeight = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();

                    if (_sendingTxHash != null)
                    {
                        if (_stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(_sendingTxHash) ==
                            null)
                        {
                            Logger.LogInformation(
                                $"Transaction {_sendingTxHash.ToHex()} submitted, waiting for including in block");
                            Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                            continue;
                        }

                        _sendingTxHash = null;
                    }

                    var stake = _systemContractReader.GetStake().ToBigInteger();
                    var isStaker = !stake.IsZero;

                    Logger.LogInformation($"Validators: {_stateManager.CurrentSnapshot.Validators.GetValidatorsPublicKeys().ToArray()}");

                    if (!isStaker)
                    {
                        var temp = _stateManager.CurrentSnapshot.Validators.GetStakerAddress();
                        
                        var coverFeesAmount = new BigInteger(10) * BigInteger.Pow(10, 18);
                        Logger.LogInformation($"Trying to become staker");
                        var balance =
                            _stateManager.CurrentSnapshot.Balances.GetBalance(_systemContractReader.NodeAddress());
                        Logger.LogInformation($"Balance is {balance.ToWei()}");
                        Logger.LogInformation($"Stake size is {_stakeSize ?? new BigInteger(0)}");
                        var isEnoughBalance = balance.ToWei() >
                                              (_stakeSize ?? StakingContract.TokenUnitsInRoll) + coverFeesAmount;
                        if (isEnoughBalance)
                        {
                            var rolls = (_stakeSize ?? (balance.ToWei() - coverFeesAmount)) /
                                        StakingContract.TokenUnitsInRoll;
                            Logger.LogInformation($"Sending transaction to become staker for {rolls} rolls");
                            BecomeStaker(rolls * StakingContract.TokenUnitsInRoll);
                            _stakeSize = null;
                            continue;
                        }

                        Logger.LogInformation($"Not enough balance to become staker");
                        continue;
                    }

                    var requestCycle = _systemContractReader.GetWithdrawRequestCycle();
                    if (requestCycle != 0)
                    {
                        Logger.LogInformation(
                            $"Stake withdrawal triggered externally in cycle {requestCycle}. Processing withdrawal...");
                        _withdrawTriggered = true;
                        continue;
                    }

                    if (_systemContractReader.IsAttendanceDetectionPhase() &&
                        _systemContractReader.IsPreviousValidator() && !_systemContractReader.IsCheckedIn())
                    {
                        Logger.LogInformation(
                            $"The node is previous validator. Trying to submit attendance detection.");
                        SubmitAttendanceDetection();
                        continue;
                    }

                    if (_systemContractReader.IsNextValidator())
                    {
                        Logger.LogDebug($"The node chosen as next validator. Nothing to do.");
                        passingCycle = GetCurrentCycle();
                        continue;
                    }

                    if (!_systemContractReader.IsAbleToBeValidator() || !_systemContractReader.IsVrfSubmissionPhase())
                    {
                        Logger.LogInformation($"Current submission phase missed. Waiting for the next one.");
                        passingCycle = GetCurrentCycle();
                        continue;
                    }

                    var (isWinner, proof) = GetVrfProof(stake);
                    if (isWinner)
                    {
                        Logger.LogDebug(
                            $"The node won the VRF lottery. Submitting transaction to become the next cycle validator");
                        SubmitVrf(proof);
                        continue;
                    }

                    Logger.LogInformation($"The node didn't win the VRF lottery. Waiting for the next cycle.");
                    passingCycle = GetCurrentCycle();
                }

                lastCheckedBlockHeight = 0;
                passingCycle = -1;

                // Try to withdraw stake
                while (!_systemContractReader.GetStake().IsZero())
                {
                    if (_stopRequested)
                        break;
                    if (_sendingTxHash != null)
                    {
                        if (_stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(_sendingTxHash) ==
                            null)
                        {
                            Logger.LogInformation(
                                $"Transaction {_sendingTxHash.ToHex()} submitted, waiting for including in block");
                            Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                            continue;
                        }

                        _sendingTxHash = null;
                    }

                    if (lastCheckedBlockHeight == _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() ||
                        GetCurrentCycle() == passingCycle)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(checkInterval));
                        continue;
                    }

                    Logger.LogWarning($"Trying to withdraw stake");

                    lastCheckedBlockHeight = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();

                    if (_systemContractReader.IsAttendanceDetectionPhase() &&
                        _systemContractReader.IsPreviousValidator() && !_systemContractReader.IsCheckedIn())
                    {
                        Logger.LogInformation(
                            $"The node is previous validator. Trying to submit attendance detection.");
                        SubmitAttendanceDetection();
                        continue;
                    }

                    var requestCycle = _systemContractReader.GetWithdrawRequestCycle();
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

                    if (GetCurrentCycle() <= requestCycle)
                    {
                        Logger.LogInformation(
                            $"Stake withdrawal request in cycle {requestCycle}, current cycle is {GetCurrentCycle()}. " +
                            $"Waiting for the next cycle to withdraw stake..."
                        );
                        passingCycle = GetCurrentCycle();
                        continue;
                    }

                    if (!IsWithdrawalPhase())
                    {
                        Logger.LogWarning($"Waiting for withdrawal phase...");
                        continue;
                    }

                    WithdrawStakeTx();
                    Logger.LogWarning(
                        $"Stake withdrawal transaction submitted. Waiting for the next block to ensure withdrawal succeeded.");
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
            var tx = _transactionBuilder.InvokeTransaction(
                _systemContractReader.NodeAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodBecomeStaker,
                _nodePublicKey ?? _systemContractReader.NodePublicKey(),
                (object) stakeAmount.ToUInt256()
            );

            AddTxToPool(tx);
        }

        public void StakeDelegation(UInt256 stakeAmount, UInt160 stakerAddress, byte[] validatorPubKey)
        {
            var tx = _transactionBuilder.InvokeTransaction(
                stakerAddress,
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodBecomeStaker,
                validatorPubKey,
                (object) stakeAmount
            );

            AddTxToPool(tx);
        }

        private void SubmitVrf(byte[] proof)
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _systemContractReader.NodeAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodSubmitVrf,
                _nodePublicKey ?? _systemContractReader.NodePublicKey(),
                (object) proof
            );

            AddTxToPool(tx);
        }

        private void SubmitAttendanceDetection()
        {
            var previousValidators = _systemContractReader.GetPreviousValidators();

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
                var previousCycle = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() /
                    StakingContract.CycleDuration - 1;
                attendances[i] = new BigInteger(attendanceData.GetAttendanceForCycle(publicKey, previousCycle))
                    .ToUInt256();
                publicKeys[i] = publicKey;
            }

            var tx = _transactionBuilder.InvokeTransaction(
                _systemContractReader.NodeAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodSubmitAttendanceDetection,
                publicKeys,
                attendances
            );
            _validatorAttendanceRepository.SaveState(attendanceData.ToBytes());
            AddTxToPool(tx);
        }

        private void AddTxToPool(Transaction tx)
        {
            var receipt = _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair);
            _sendingTxHash = tx.FullHash(receipt.Signature);
            var result = _transactionPool.Add(receipt);
            Logger.LogDebug(result == OperatingError.Ok
                ? $"Transaction successfully submitted: {receipt.Hash.ToHex()}"
                : $"Cannot add tx to pool: {result}");
        }

        private void RequestStakeWithdrawal()
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _systemContractReader.NodeAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodRequestStakeWithdrawal,
                _systemContractReader.NodePublicKey()
            );

            AddTxToPool(tx);
        }

        private void WithdrawStakeTx()
        {
            var tx = _transactionBuilder.InvokeTransaction(
                _systemContractReader.NodeAddress(),
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodWithdrawStake,
                _systemContractReader.NodePublicKey()
            );

            AddTxToPool(tx);
        }

        private (bool, byte[]) GetVrfProof(BigInteger stake)
        {
            var seed = _systemContractReader.GetVrfSeed();
            var rolls = stake / StakingContract.TokenUnitsInRoll;
            var totalRolls = _systemContractReader.GetTotalStake().ToBigInteger() / StakingContract.TokenUnitsInRoll;
            var (proof, value, j) = Vrf.Evaluate(_privateWallet.EcdsaKeyPair.PrivateKey.Buffer.ToByteArray(), seed,
                StakingContract.Role, StakingContract.ExpectedValidatorsCount, rolls, totalRolls);
            return (j > 0, proof);
        }

        private bool IsNextValidator()
        {
            var stakerPublicKey = _systemContractReader.NodePublicKey();
            return _systemContractReader.IsNextValidator(stakerPublicKey);
        }

        private bool IsWithdrawalPhase()
        {
            var blockNumber = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var blockInCycle = blockNumber % StakingContract.CycleDuration;
            return blockInCycle >= StakingContract.AttendanceDetectionDuration;
        }

        private int GetCurrentCycle()
        {
            var blockNumber = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var currentCycle = blockNumber / StakingContract.CycleDuration;
            return (int) currentCycle;
        }

        public void WithdrawStakeAndStop()
        {
            Logger.LogDebug("Withdrawing stake and stopping validation");
            _withdrawTriggered = true;
        }

        public bool IsStarted()
        {
            return _started;
        }

        public bool IsWithdrawTriggered()
        {
            return _withdrawTriggered;
        }

        private ValidatorAttendance? GetValidatorAttendance()
        {
            var bytes = _validatorAttendanceRepository.LoadState();
            if (bytes is null || bytes.Length == 0) return null;
            var cycle = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() / StakingContract.CycleDuration;
            var indexInCycle = _stateManager.CurrentSnapshot.Blocks.GetTotalBlockHeight() %
                               StakingContract.CycleDuration;
            return ValidatorAttendance.FromBytes(bytes, cycle,
                indexInCycle >= StakingContract.AttendanceDetectionDuration);
        }
    }
}