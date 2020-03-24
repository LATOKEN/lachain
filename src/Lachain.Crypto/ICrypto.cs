namespace Lachain.Crypto
{
    public interface ICrypto
    {
        /// <summary>
        /// Check ECDSA Signature (secp256k1)
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="signature">Signature</param>
        /// <param name="publicKey">Public Key</param>
        /// <returns>Bool</returns>
        bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey);

        /// <summary>
        /// Sign already hashed message (secp256k1)
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="privateKey">Private Key</param>
        /// <returns>Signature bytearray</returns>
        byte[] Sign(byte[] message, byte[] privateKey);
        
        /// <summary>
        /// Recovers public key from signature
        /// </summary>
        /// <param name="message"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        byte[] RecoverSignature(byte[] message, byte[] signature);
        
        /// <summary>
        /// Computes address from public key
        /// </summary>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        byte[] ComputeAddress(byte[] publicKey);
        
        /// <summary>
        /// Derive Public Key from private
        /// </summary>
        /// <param name="privateKey">Private Key</param>
        /// <param name="compress">Compress pubkey</param>
        /// <returns>Bytearray Public Key</returns>
        byte[] ComputePublicKey(byte[] privateKey, bool compress);

        /// <summary>
        /// Generates random bytes
        /// </summary>
        /// <param name="length">Length</param>
        /// <returns>Random bytearray</returns>
        byte[] GenerateRandomBytes(int length);
        
        /// <summary>
        /// Recovers public key from signature
        /// </summary>
        /// <param name="messageHash"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        byte[] RecoverSignatureHashed(byte[] messageHash, byte[] signature);
        
        /// <summary>
        /// Check ECDSA Signature (secp256k1)
        /// </summary>
        /// <param name="messageHash">Message</param>
        /// <param name="signature">Signature</param>
        /// <param name="publicKey">Public Key</param>
        /// <returns>Bool</returns>
        bool VerifySignatureHashed(byte[] messageHash, byte[] signature, byte[] publicKey);
    }
}