using System.Collections.Generic;
using Phorkus.Hermes.Signer;
using Phorkus.Hermes.Signer.Messages;

namespace Phorkus.Hermes
{
    public interface ISignerProtocol
    {
        SignerState CurrentState { get; }
        
        void Initialize(byte[] message);
        
        Round1Message Round1();
        
        Round2Message Round2(IEnumerable<Round1Message> round1Messages);

        Round3Message Round3(IEnumerable<Round2Message> round2Messages);

        Round4Message Round4(IEnumerable<Round3Message> round3Messages);

        Round5Message Round5(IEnumerable<Round4Message> round4Messages);

        Round6Message Round6(IEnumerable<Round5Message> round5Messages);
        
        DSASignature Finalize(IEnumerable<Round6Message> round6Messages);
    }
}