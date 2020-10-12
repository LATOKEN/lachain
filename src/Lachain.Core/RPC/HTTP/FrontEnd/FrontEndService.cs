using System;
using System.Linq;
using System.Numerics;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP.FrontEnd
{
    public class FrontEndService : JsonRpcService
    {
        private static readonly ILogger<FrontEndService> Logger = LoggerFactory.GetLoggerForClass<FrontEndService>();

        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ISystemContractReader _systemContractReader;
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly ILocalTransactionRepository _localTransactionRepository;

        public FrontEndService(
            IStateManager stateManager,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ISystemContractReader systemContractReader,
            ILocalTransactionRepository localTransactionRepository,
            IValidatorStatusManager validatorStatusManager,
            IPrivateWallet privateWallet
        )
        {
            _stateManager = stateManager;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _systemContractReader = systemContractReader;
            _localTransactionRepository = localTransactionRepository;
            _validatorStatusManager = validatorStatusManager;
            _privateWallet = privateWallet;
        }

        [JsonRpcMethod("fe_getBalance")]
        private JObject GetBalance(string address)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney();
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                addressUint160);
            return new JObject
            {
                ["balance"] = balance.ToString(),
                ["stake"] = stake.ToString(),
                ["penalty"] = penalty.ToString(),
                ["nonce"] = nonce,
            };
        }

        [JsonRpcMethod("eth_coinbase")]
        private string GetCoinBase()
        {
            return _systemContractReader.NodeAddress().ToHex();
        }

        [JsonRpcMethod("fe_account")]
        private JObject GetAccount(string? address = null)
        {
            address ??= _systemContractReader.NodeAddress().ToHex();
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney().ToWei() /
                        StakingContract.TokenUnitsInRoll;
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();
            var isCurrentValidator = _stateManager.CurrentSnapshot.Validators
                .GetValidatorsPublicKeys().Any(pk =>
                    pk.Buffer.ToByteArray().SequenceEqual(_systemContractReader.NodePublicKey()));
            var isNextValidator = _systemContractReader.IsNextValidator();
            var isPreviousValidator = _systemContractReader.IsPreviousValidator();
            var isAbleToBeValidator = _systemContractReader.IsAbleToBeValidator();
            var isStaker = !_systemContractReader.GetStake().IsZero();
            var isAbleToBeStaker = balance.ToWei() > StakingContract.TokenUnitsInRoll;

            var isWalletLocked = _privateWallet.IsLocked();

            var withdrawTriggered = _validatorStatusManager.IsWithdrawTriggered();
            var isValidatorStatusManagerActive = _validatorStatusManager.IsStarted();
            var withdrawRequestCycle = _systemContractReader.GetWithdrawRequestCycle();

            string state;
            if (isValidatorStatusManagerActive && withdrawTriggered)
            {
                if (isNextValidator)
                {
                    state = "StakeReserved";
                }
                else if (withdrawRequestCycle == 0)
                {
                    state = "SubmittingWithdrawRequest";
                }
                else
                    state = "WaitingForTheNextCycleToWithdraw";
            }
            else if (isCurrentValidator)
                state = "Validator";
            else if (isNextValidator)
                state = "NextValidator";
            else if (isAbleToBeValidator)
                state = "AbleToBeValidator";
            else if (isPreviousValidator)
                state = "PreviousValidator";
            else if (isAbleToBeStaker)
                state = "AbleToBeStaker";
            else state = "Newbie";

            return new JObject
            {
                ["address"] = address,
                ["balance"] = balance.ToString(),
                ["stake"] = stake.ToString(),
                ["penalty"] = penalty.ToString(),
                ["state"] = state,
                ["online"] = isStaker,
                ["isWalletLocked"] = isWalletLocked,
            };
        }

        [JsonRpcMethod("fe_phase")]
        private JObject GetCurrentPhase()
        {
            var attendanceDetectionPhase = _systemContractReader.IsAttendanceDetectionPhase();
            var vrfSubmissionPhase = _systemContractReader.IsVrfSubmissionPhase();
            var keyGenPhase = _systemContractReader.IsKeyGenPhase();

            return new JObject
            {
                ["AttendanceSubmissionPhase"] = attendanceDetectionPhase,
                ["VrfSubmissionPhase"] = vrfSubmissionPhase,
                ["KeyGenPhase"] = keyGenPhase,
            };
        }

        [JsonRpcMethod("fe_unlock")]
        private string UnlockWallet(string password, long s)
        {
            return _privateWallet.Unlock(password, s) ? "unlocked" : "incorrect_password";
        }

        [JsonRpcMethod("fe_isLocked")]
        private string IsWalletLocked()
        {
            return _privateWallet.IsLocked() ? "0x1" : "0x0";
        }

        [JsonRpcMethod("fe_sendTransaction")]
        private string SendTransaction(JObject opts)
        {
            var from = opts["from"]?.ToString().HexToBytes().ToUInt160() ??
                       throw new Exception($"\"from\" {opts["from"]} is not valid");
            var to = opts["to"]?.ToString().HexToBytes().ToUInt160() ??
                     throw new Exception($"\"to\" {opts["from"]} is not valid");
            var value = Money.Parse(opts["amount"]?.ToString() ??
                                    throw new Exception($"\"amount\" {opts["amount"]} is not valid")
            );
            var nonce = _transactionPool.GetNextNonceForAddress(from);
            var tx = new Transaction
            {
                To = to,
                From = from,
                GasPrice = (ulong) _stateManager.CurrentSnapshot.NetworkGasPrice,
                /* TODO: "calculate gas limit for input size" */
                GasLimit = 10000000,
                Nonce = nonce,
                Value = value.ToUInt256(false)
            };

            return AddTxToPool(tx);
        }

        [JsonRpcMethod("fe_transactions")]
        private JObject GetLocalTransactions(JObject opts)
        {
            var limit = ulong.Parse(opts["count"]?.ToString());

            var results = new JArray();
            var txHashes = _localTransactionRepository.GetTransactionHashes(limit);
            foreach (var txHash in txHashes)
            {
                var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash);

                if (receipt is null) continue;
                var txFormatted = FormatTx(receipt,
                    _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt.Block)!); 
                results.Add(txFormatted);
            }

            return new JObject
            {
                ["transactions"] = results,
            };
        }

        [JsonRpcMethod("fe_pendingTransactions")]
        private JObject GetPendingTransactions(JObject opts)
        {
            var address = opts["address"]?.ToString().HexToBytes().ToUInt160() ??
                          throw new Exception($"\"address\" {opts["address"]} is not valid");
            var limit = ulong.Parse(opts["count"]?.ToString());

            var results = new JArray();
            var poolTxs = _transactionPool.Transactions.Values;
            foreach (var tx in poolTxs)
            {
                if (tx.Transaction.From.Equals(address))
                {
                    results.Add(FormatTx(tx));
                    if (results.Count == (int) limit)
                        break;
                }
            }

            return new JObject
            {
                ["transactions"] = results,
            };
        }

        private static JObject FormatTx(TransactionReceipt receipt, Block? block = null)
        {
            return new JObject
            {
                ["hash"] = receipt.Hash.ToHex(),
                ["type"] = "send",
                ["from"] = receipt.Transaction.From.ToHex(),
                ["to"] = receipt.Transaction.To.ToHex(),
                ["amount"] = receipt.Transaction.Value.ToMoney().ToString(),
                ["usedFee"] = block is null
                    ? "0"
                    : new Money(new BigInteger(receipt.GasUsed) * receipt.Transaction.GasPrice).ToString(),
                ["maxFee"] = new Money(new BigInteger(receipt.Transaction.GasLimit) * receipt.Transaction.GasPrice)
                    .ToString(),
                ["nonce"] = receipt.Transaction.Nonce,
                ["cycle"] = receipt.Block / StakingContract.CycleDuration,
                ["blockHash"] = block is null
                    ? "0x0000000000000000000000000000000000000000000000000000000000000000"
                    : block.Hash.ToHex(),
                ["payLoad"] = receipt.Transaction.Invocation.ToHex(),
                ["timestamp"] = block?.Timestamp / 1000 ?? 0,
            };
        }

        private string AddTxToPool(Transaction tx)
        {
            var wallet = _privateWallet.GetWalletInstance();
            if (wallet is null) return "0x0";
            var receipt = _transactionSigner.Sign(tx, wallet.EcdsaKeyPair);
            var result = _transactionPool.Add(receipt);
            Logger.LogDebug(result == OperatingError.Ok
                ? $"Transaction successfully submitted: {receipt.Hash.ToHex()}"
                : $"Cannot add tx to pool: {result}");
            return result == OperatingError.Ok ? receipt.Hash.ToHex() : "0x0";
        }
    }
}