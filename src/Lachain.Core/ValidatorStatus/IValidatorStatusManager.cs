using Lachain.Proto;

namespace Lachain.Core.ValidatorStatus
{
    public interface IValidatorStatusManager
    {
        void Start(bool isWithdrawTriggered);

        void StartWithStake(UInt256 stake);
    
        bool IsStarted();
    
        bool IsWithdrawTriggered();
        
        void WithdrawStakeAndStop();
    }
}