using AustinHarris.JsonRpc;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Vault;
using Lachain.Logger;
using Lachain.Utility;
using System;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class ValidatorServiceWeb3 : JsonRpcService
    {
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly IPrivateWallet _privateWallet;
        private ITransactionBuilder _transactionBuilder;

        private static readonly ILogger<TransactionServiceWeb3> Logger =
            LoggerFactory.GetLoggerForClass<TransactionServiceWeb3>();

        public ValidatorServiceWeb3(
            IValidatorStatusManager validatorStatusManager, IPrivateWallet privateWallet,
            ITransactionBuilder transactionBuilder)
        {
            _validatorStatusManager = validatorStatusManager;
            _privateWallet = privateWallet;
            _transactionBuilder = transactionBuilder;
        }


        // opts["stakerAddress"] = staker address in hex format
        // opts["validatorPublicKey"] = validator public key in hex format
        // opts["stakeAmount"] = stake amount in LA in decimal format

        [JsonRpcMethod("la_getStakeTransaction")]
        public JObject GetStakeTransaction(JObject opts) 
        {
            var staker = opts["stakerAddress"]?.ToString().HexToBytes().ToUInt160() ?? 
                    throw new Exception($"\"stakerAddress\" {opts["stakerAddress"]} is not valid");

            var validatorPubKey = opts["validatorPublicKey"]?.ToString().HexToBytes() ??
                    throw new Exception($"\"validatorPublicKey\" {opts["validatorPublicKey"]} is not valid");

            var stakeAmount = Money.Parse(opts["stakeAmount"]?.ToString() ??
                                    throw new Exception($"\"stakeAmount\" {opts["stakeAmount"]} is not valid")
            );
            var tx = _transactionBuilder.InvokeTransaction(
                staker,
                ContractRegisterer.StakingContract,
                Money.Zero,
                StakingInterface.MethodBecomeStaker,
                validatorPubKey,
                (object) stakeAmount.ToUInt256()
            );

            return Web3DataFormatUtils.Web3UnsignedTransaction(tx);
        }

        [JsonRpcMethod("validator_start")]
        public string StartValidator()
        {
            if (_privateWallet.GetWalletInstance() is null) 
                return "wallet_locked";

            Logger.LogDebug("validator start command received");
            if (_validatorStatusManager.IsStarted())
                return "withdraw previous stake first"; 
            
            _validatorStatusManager.Start(false);

            return "validator_started";
        }

        [JsonRpcMethod("validator_start_with_stake")]
        public string StartValidatorWithStake(string stake)
        {
            if (_privateWallet.GetWalletInstance() is null) return "wallet_locked";

            Logger.LogDebug("validator start_with_stake command received");
            if (_validatorStatusManager.IsStarted())
                return "withdraw previous stake first";

            _validatorStatusManager.StartWithStake(Money.Parse(stake).ToUInt256());

            return "validator_started";
        }

        [JsonRpcMethod("validator_status")]
        public string GetValidatorStatus()
        {
            if (!_validatorStatusManager.IsStarted()) return "0x00";
            return _validatorStatusManager.IsWithdrawTriggered() ? "0x002" : "0x01";
        }

        [JsonRpcMethod("eth_mining")]
        public bool IsMining()
        {
            return _validatorStatusManager.IsStarted();
        }

        [JsonRpcMethod("validator_stop")]
        public string StopValidator()
        {
            if (_privateWallet.GetWalletInstance() is null) return "wallet_locked";

            Logger.LogDebug("validator stop command received");
            _validatorStatusManager.WithdrawStakeAndStop();
            return "validator_stopped";
        }
    }
}