using Lachain.Core.Blockchain.Error;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IMultisigVerifier
    {
        OperatingError VerifyMultisig(MultiSig multisig, UInt256 hash, bool useNewChainId);
    }
}