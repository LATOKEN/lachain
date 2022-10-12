using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;
using Nethereum.Signer;
using Transaction = Lachain.Proto.Transaction;

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
        private readonly ITransactionManager _transactionManager;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public FrontEndService(
            IStateManager stateManager,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ISystemContractReader systemContractReader,
            ILocalTransactionRepository localTransactionRepository,
            IValidatorStatusManager validatorStatusManager,
            IPrivateWallet privateWallet,
            ITransactionManager transactionManager
        )
        {
            _stateManager = stateManager;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _systemContractReader = systemContractReader;
            _localTransactionRepository = localTransactionRepository;
            _validatorStatusManager = validatorStatusManager;
            _privateWallet = privateWallet;
            _transactionManager = transactionManager;
        }

        [JsonRpcMethod("fe_getBalance")]
        private JObject GetBalance(string address)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var staked = _systemContractReader.GetStake(addressUint160).ToMoney();
            var staking = _systemContractReader.GetStakerTotalStake(addressUint160).ToMoney();
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                addressUint160);
            return new JObject
            {
                ["balance"] = balance.ToString(),
                ["staked"] = staked.ToString(),
                ["staking"] = staking.ToString(),
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
            var publicKey = _systemContractReader.NodePublicKey().ToHex();
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var staked = _systemContractReader.GetStake(addressUint160).ToMoney();
            var staking = _systemContractReader.GetStakerTotalStake(addressUint160).ToMoney();
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
                ["publicKey"] = publicKey,
                ["balance"] = balance.ToString(),
                ["staked"] = staked.ToString(),
                ["staking"] = staking.ToString(),
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
        public string UnlockWallet(string password, long s)
        {
            return _privateWallet.Unlock(password, s) ? "unlocked" : "incorrect_password";
        }

        [JsonRpcMethod("fe_changePassword")]
        public string ChangePassword(string currentPassword, string newPassword)
        {
            return _privateWallet.ChangePassword(currentPassword, newPassword) ? "password_changed" : "incorrect_current_password";
        }
        
        [JsonRpcMethod("fe_isLocked")]
        public string IsWalletLocked()
        {
            return _privateWallet.IsLocked() ? "0x1" : "0x0";
        }

        [JsonRpcMethod("fe_sendTransaction")]
        private string SendTransaction(JObject opts)
        {
            var from = opts["from"]?.ToString().HexToBytes().ToUInt160() ??
                       _systemContractReader.NodeAddress();
            var to = opts["to"]?.ToString().HexToBytes().ToUInt160() ??
                     throw new Exception($"\"to\" {opts["to"]} is not valid");
            var value = Money.Parse(opts["amount"]?.ToString() ??
                                    throw new Exception($"\"amount\" {opts["amount"]} is not valid")
            );
            var invocation = opts["data"]?.ToString().HexToBytes();
            var nonce = _transactionPool.GetNextNonceForAddress(from);
            var tx = new Transaction
            {
                To = to,
                From = from,
                GasPrice = (ulong) _stateManager.CurrentSnapshot.NetworkGasPrice,
                /* TODO: "calculate gas limit for input size" */
                GasLimit = 10000000,
                Nonce = nonce,
                Value = value.ToUInt256(),
                Invocation = ByteString.CopyFrom(invocation)
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
        
        [JsonRpcMethod("fe_larcHistory")]
        private JObject GetLarcHistory(JObject opts)
        {
            var address = opts["address"]?.ToString().HexToBytes().ToUInt160() ??
                          throw new Exception($"\"address\" {opts["address"]} is not valid");
            var limit = ulong.Parse(opts["count"]?.ToString());

            var results = new JArray();
            var txHashes = _localTransactionRepository.GetTransactionHashes(limit);
            
            foreach (var txHash in txHashes)
            {
                var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash);

                if (receipt is null || !receipt.Transaction.To.Equals(address)) continue;
                var txFormatted = FormatTx(receipt,
                    _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt.Block)!); 
                results.Add(txFormatted);
            }

            return new JObject
            {
                ["transactions"] = results,
            };
        }

        [JsonRpcMethod("fe_signMessage")]
        public string SignMessage(string message, bool useNewChainId)
        {
            if (_privateWallet.IsLocked())
            {
                throw new Exception("wallet is locked");
            }
            var keyPair = _privateWallet.EcdsaKeyPair;
            Logger.LogInformation($"public key: {keyPair.PublicKey.ToHex()}, address: {keyPair.PublicKey.GetAddress().ToHex()}");
            try
            {
                var msg = message.HexToBytes();
                var signature = Crypto.Sign(msg, keyPair.PrivateKey.Encode(), useNewChainId);
                return Web3DataFormatUtils.Web3Data(signature);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to sign message: {exception}");
                throw;
            }
        }

        [JsonRpcMethod("fe_verifySign")]
        public bool VerifySign(string message, string sign, bool useNewChainId)
        {
            if (_privateWallet.IsLocked())
            {
                throw new Exception("wallet is locked");
            }
            var keyPair = _privateWallet.EcdsaKeyPair;
            Logger.LogInformation($"public key: {keyPair.PublicKey.ToHex()}, address: {keyPair.PublicKey.GetAddress().ToHex()}");
            try
            {
                var msg = message.HexToBytes();
                var signBytes = sign.HexToBytes();
                var pubkeyParsed = Crypto.RecoverSignature(msg, signBytes, useNewChainId);
                if (!keyPair.PublicKey.EncodeCompressed().SequenceEqual(pubkeyParsed)) return false;
                return Crypto.VerifySignature(msg, signBytes, keyPair.PublicKey.EncodeCompressed(), useNewChainId);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to verify signed message: {exception}");
                throw;
            }
        }

        [JsonRpcMethod("fe_verifyRawTransaction")]
        public JObject VerifyRawTransaction(string rawTx, string externalTxHash, bool useNewChainId)
        {
            var ethTx = new LegacyTransactionChainId(rawTx.HexToBytes());

            var r = ethTx.Signature.R;
            while (r.Length < 32)
                r = "00".HexToBytes().Concat(r).ToArray();

            var s = ethTx.Signature.S;
            while (s.Length < 32)
                s = "00".HexToBytes().Concat(s).ToArray();

            var v = ethTx.Signature.V;
            var decodedV = DecodeV(v);

            var signature = r.Concat(s).Concat(v).ToArray().ToSignature(useNewChainId);
            try
            {
                var transaction = MakeTransaction(ethTx);
                var txHash = transaction.FullHash(signature, useNewChainId);
                if (!txHash.ToBytes().SequenceEqual(externalTxHash.HexToBytes()))
                {
                    return FormatResult($"tx hash mismatch, calculated hash: {txHash.ToHex()}" , UInt256Utils.Zero, 0);
                }
                var receipt = new TransactionReceipt
                {
                    Hash = txHash,
                    Signature = signature,
                    Status = TransactionStatus.Pool,
                    Transaction = transaction
                };
                var publicKey = receipt.RecoverPublicKey(useNewChainId);
                var address = publicKey.GetAddress();
                if (!address.Equals(receipt.Transaction.From))
                    return FormatResult($"Address mismatch, got address: {address.ToHex()}", UInt256Utils.Zero, null);
                var result = _transactionManager.Verify(receipt, useNewChainId);

                if (result != OperatingError.Ok) return FormatResult($"Transaction is invalid: {result}" , UInt256Utils.Zero, 0);
                return FormatResult("transaction verified", txHash, decodedV);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Exception in handling fe_verifyRawTransaction: {e}");
                throw;
            }
        }

        int? DecodeV(byte[]? v)
        {
            if (v is null) return null;
            var list = new List<byte>();
            foreach (var item in v)
            {
                list.Add(item);
            }
            var reversed = list.ToArray().Reverse().ToList();
            while(reversed.Count < 4) reversed.Add(0);
            return BitConverter.ToInt32(reversed.ToArray());
        }

        public JObject FormatResult(string msg, UInt256 hash, int? v)
        {
            if (v is null) v = 0;
            return new JObject
            {
                ["message"] = msg,
                ["hash"] = Web3DataFormatUtils.Web3Data(hash),
                ["v"] = Web3DataFormatUtils.Web3Number((ulong) v),
            };
        }
        public Transaction MakeTransaction(LegacyTransactionChainId ethTx)
        {
            return new Transaction
            {
                // this is special case where empty uint160 is allowed
                To = ethTx.ReceiveAddress?.ToUInt160() ?? UInt160Utils.Empty,
                Value = ethTx.Value.ToUInt256(true),
                From = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160(),
                Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16),
                Invocation = ethTx.Data is null ? ByteString.Empty : ByteString.CopyFrom(ethTx.Data),
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
            var receipt = _transactionSigner.Sign(tx, wallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() + 1));
            var result = _transactionPool.Add(receipt);
            Logger.LogDebug(result == OperatingError.Ok
                ? $"Transaction successfully submitted: {receipt.Hash.ToHex()}"
                : $"Cannot add tx to pool: {result}");
            return result == OperatingError.Ok ? receipt.Hash.ToHex() : "0x0";
        }
    }
}