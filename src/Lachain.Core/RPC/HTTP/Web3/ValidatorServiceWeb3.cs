using AustinHarris.JsonRpc;
using Lachain.Core.ValidatorStatus;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class ValidatorServiceWeb3 : JsonRpcService
    {
        private readonly IValidatorStatusManager _validatorStatusManager;
        
        public ValidatorServiceWeb3(
            IValidatorStatusManager validatorStatusManager
            )
        {
            _validatorStatusManager = validatorStatusManager;
        }

        [JsonRpcMethod("validator_start")]
        private string StartValidator()
        {
            if (!_validatorStatusManager.IsStarted())
            {
                _validatorStatusManager.Start(false);
            }

            return "0x1";
        }

        [JsonRpcMethod("validator_stop")]
        private string StopValidator()
        {
            _validatorStatusManager.WithdrawStakeAndStop();
            return "0x1";
        }
    }
}