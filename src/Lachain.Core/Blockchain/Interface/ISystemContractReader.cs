using Lachain.Crypto.ECDSA;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ISystemContractReader
    {
        UInt256 GetStake(UInt160 stakerAddress = null);
        
        UInt256 GetPenalty(UInt160 stakerAddress = null);
        
        UInt256 GetTotalStake();

        byte[] GetVRFSeed();

        int GetWithdrawRequestCycle(UInt160 stakerAddress = null);

        bool IsNextValidator(byte[] stakerPublicKey = null);

        bool IsVrfSubmissionPhase();

        bool IsAttendanceDetectionPhase();

        bool IsKeyGenPhase();
        
        bool IsCheckedIn(byte[] stakerAddress = null);

        bool IsPreviousValidator(byte[] stakerPublicKey = null);

        byte[][] GetPreviousValidators();

        bool IsAbleToBeValidator(UInt160 stakerAddress = null);

        UInt160 NodeAddress();

        byte[] NodePublicKey();

    }
}