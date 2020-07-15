using AustinHarris.JsonRpc;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Logger;

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
            if (_privateWallet.GetWalletInstance() is null) return "wallet_locked";

            Logger.LogDebug("validator start command received");
            if (!_validatorStatusManager.IsStarted())
            {
                _validatorStatusManager.Start(false);
            }

            return "validator_started";
        }

        [JsonRpcMethod("validator_status")]
        private string GetValidatorStatus()
        {
            return _validatorStatusManager.IsStarted() ? "0x01" : "0x00";
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