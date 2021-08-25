using System;
using Lachain.Proto;

namespace Lachain.Core.ValidatorStatus
{
    public interface IValidatorStatusManager : IDisposable
    {
        void Start(bool isWithdrawTriggered);

        void StartWithStake(UInt256 stake, byte[] address = null!);
    
        bool IsStarted();
    
        bool IsWithdrawTriggered();

        void WithdrawStakeAndStop();

        void Stop();
    }
}