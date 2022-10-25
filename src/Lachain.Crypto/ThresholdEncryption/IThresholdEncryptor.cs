using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Crypto.ThresholdEncryption
{
    public interface IThresholdEncryptor
    {
        EncryptedShare Encrypt(IRawShare rawShare);
        List<PartiallyDecryptedShare> AddEncryptedShares(List<EncryptedShare> encrypedShares);
        bool AddDecryptedShare(TPKEPartiallyDecryptedShareMessage msg, int senderId);
        bool CheckDecryptedShares(int id);
        bool GetResult(out ISet<IRawShare>? result);
    }
}