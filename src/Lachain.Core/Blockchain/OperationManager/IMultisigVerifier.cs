using Lachain.Proto;

namespace Lachain.Core.Blockchain.OperationManager
{
    public interface IMultisigVerifier
    {
        OperatingError VerifyMultisig(MultiSig multisig, UInt256 hash);
    }
}