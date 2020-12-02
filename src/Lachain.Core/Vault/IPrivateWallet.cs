using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;

namespace Lachain.Core.Vault
{
    public interface IPrivateWallet
    {
        EcdsaKeyPair EcdsaKeyPair { get; }

        Crypto.TPKE.PrivateKey? GetTpkePrivateKeyForBlock(ulong block);
        
        void AddTpkePrivateKeyAfterBlock(ulong block, Crypto.TPKE.PrivateKey key);

        Crypto.ThresholdSignature.PrivateKeyShare? GetThresholdSignatureKeyForBlock(ulong block);
        
        void AddThresholdSignatureKeyAfterBlock(ulong block, Crypto.ThresholdSignature.PrivateKeyShare key);

        IPrivateWallet? GetWalletInstance();

        bool Unlock(string password, long ms);
        
        bool IsLocked();
        bool HasKeyForKeySet(PublicKeySet thresholdSignaturePublicKeySet);
    }
}