using Phorkus.Hermes.Signer;

namespace Phorkus.Hermes
{
    public interface ISignerProtocol
    {
        SignerState CurrentState { get; }

        void Initialize();
    }
}