using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;

namespace Lachain.Core.Vault
{
    public interface IPrivateWallet
    {
        EcdsaKeyPair EcdsaKeyPair { get; }

        PrivateKey? GetTpkePrivateKeyForBlock(ulong block);
        
        void AddTpkePrivateKeyAfterBlock(ulong block, PrivateKey key);

        PrivateKeyShare? GetThresholdSignatureKeyForBlock(ulong block);
        
        void AddThresholdSignatureKeyAfterBlock(ulong block, PrivateKeyShare key);

        IPrivateWallet? GetWalletInstance();

        bool Unlock(string password, long ms);
        
        bool IsLocked();
        bool HasKeyForKeySet(PublicKeySet thresholdSignaturePublicKeySet, ulong beforeBlock);
    }
}