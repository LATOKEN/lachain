using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IMultisigVerifier
    {
        OperatingError VerifyMultisig(MultiSig multisig, UInt256 hash);
    }
}