using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ISystemContractReader
    {
        UInt256 GetStake(UInt160? stakedAddress = null);
        
        UInt256 GetStakerTotalStake(UInt160? stakedAddress = null);
        
        UInt256 GetPenalty(UInt160? stakedAddress = null);

        UInt256 GetTotalStake();

        byte[] GetVrfSeed();

        int GetWithdrawRequestCycle(UInt160? stakedAddress = null);

        bool IsNextValidator(byte[]? stakedPublicKey = null);

        bool IsVrfSubmissionPhase();

        bool IsAttendanceDetectionPhase();

        bool IsKeyGenPhase();

        bool IsCheckedIn(byte[]? stakedAddress = null);

        bool IsPreviousValidator(byte[]? stakedPublicKey = null);

        byte[][] GetPreviousValidators();

        bool IsAbleToBeValidator(UInt160? stakedAddress = null);

        UInt160 NodeAddress();

        byte[] NodePublicKey();

        ulong GetLastSuccessfulKeygenBlock();
    }
}