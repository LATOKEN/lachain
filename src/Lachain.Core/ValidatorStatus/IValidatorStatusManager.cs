using Lachain.Proto;

namespace Lachain.Core.ValidatorStatus
{
    public interface IValidatorStatusManager
    {
        void Start(bool isWithdrawTriggered);
    
        bool IsStarted();
        
        void WithdrawStakeAndStop();
    }
}