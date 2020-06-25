namespace Lachain.Core.ValidatorStatus
{
    public interface IValidatorStatusManager
    {
        void Start(bool isWithdrawTriggered);
    
        bool IsStarted();
    
        bool IsWithdrawTriggered();
        
        void WithdrawStakeAndStop();
    }
}