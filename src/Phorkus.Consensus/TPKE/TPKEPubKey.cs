using System;
using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Crypto
{
    public class TPKEPubKey
    {

        public IEncryptedShare Encrypt(IRawShare rawShare)
        {
            throw new NotImplementedException();
        }

        public IPartiallyDecryptedShare Decode(DecMessage message)
        {
            throw new NotImplementedException();
        }

        public DecMessage Encode(IPartiallyDecryptedShare share)
        {
            throw new NotImplementedException();
        }

        public IRawShare FullDecrypt(ISet<IPartiallyDecryptedShare> shares)
        {
            throw new NotImplementedException();
        }
    }
}