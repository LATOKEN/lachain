using AustinHarris.JsonRpc;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Logger;
using Lachain.Utility;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class ValidatorServiceWeb3 : JsonRpcService
    {
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly IPrivateWallet _privateWallet;

        private static readonly ILogger<TransactionServiceWeb3> Logger =
            LoggerFactory.GetLoggerForClass<TransactionServiceWeb3>();

        public ValidatorServiceWeb3(
            IValidatorStatusManager validatorStatusManager, IPrivateWallet privateWallet)
        {
            _validatorStatusManager = validatorStatusManager;
            _privateWallet = privateWallet;
        }

        [JsonRpcMethod("validator_start")]
        private string StartValidator()
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
        private string StartValidatorWithStake(string stake)
        {
            if (_privateWallet.GetWalletInstance() is null) return "wallet_locked";

            Logger.LogDebug("validator start_with_stake command received");
            if (_validatorStatusManager.IsStarted())
                return "withdraw previous stake first";

            _validatorStatusManager.StartWithStake(Money.Parse(stake).ToUInt256());

            return "validator_started";
        }

        [JsonRpcMethod("validator_status")]
        private string GetValidatorStatus()
        {
            if (!_validatorStatusManager.IsStarted()) return "0x00";
            return _validatorStatusManager.IsWithdrawTriggered() ? "0x002" : "0x01";
        }

        [JsonRpcMethod("eth_mining")]
        private bool IsMining()
        {
            return _validatorStatusManager.IsStarted();
        }

        [JsonRpcMethod("validator_stop")]
        private string StopValidator()
        {
            if (_privateWallet.GetWalletInstance() is null) return "wallet_locked";

            Logger.LogDebug("validator stop command received");
            _validatorStatusManager.WithdrawStakeAndStop();
            return "validator_stopped";
        }
    }
}