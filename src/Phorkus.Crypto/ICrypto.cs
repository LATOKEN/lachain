using System.Numerics;

namespace Phorkus.Crypto
{
    public interface ICrypto
    {
        /// <summary>
        /// Check ECDSA Signature (secp256r1)
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="signature">Signature</param>
        /// <param name="publicKey">Public Key</param>
        /// <returns>Bool</returns>
        bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey);

        /// <summary>
        /// Sign sha256 Message (secp256r1)
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="privateKey">Private Key</param>
        /// <returns>Siganture bytearray</returns>
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
        /// Decode Public Key
        /// </summary>
        /// <param name="publicKey">Data</param>
        /// <param name="compress">Compress public key</param>
        /// <param name="x">X</param>
        /// <param name="y">Y</param>
        /// <returns>Public key bytearray</returns>
        byte[] DecodePublicKey(byte[] publicKey, bool compress, out BigInteger x, out BigInteger y);

        /// <summary>
        /// Encrypt using ECB
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="key">Key</param>
        /// <returns>Bytearray</returns>
        byte[] AesEncrypt(byte[] data, byte[] key);

        /// <summary>
        /// Decrypt using ECB
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="key">Key</param>
        /// <returns>Bytearray</returns>
        byte[] AesDecrypt(byte[] data, byte[] key);

        /// <summary>
        /// Encrypt using CBC
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="key">Key</param>
        /// <param name="iv">IV</param>
        /// <returns>Bytearray</returns>
        byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv);

        /// <summary>
        /// Decrypt using CBC
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="key">Key</param>
        /// <param name="iv">IV</param>
        /// <returns>Bytearray</returns>
        byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv);

        /// <summary>
        /// Generate SCrypt key
        /// </summary>
        /// <param name="P">Password</param>
        /// <param name="S">Salt</param>
        /// <param name="N">CPU/Memory cost parameter. Must be larger than 1, a power of 2 and less than 2^(128 * r / 8).</param>
        /// <param name="r">Block size, must be >= 1.</param>
        /// <param name="p">Parallelization. Must be a positive integer less than or equal to Int32.MaxValue / (128 * r * 8).</param>
        /// <param name="dkLen">Generate key length</param>
        byte[] SCrypt(byte[] P, byte[] S, int N, int r, int p, int dkLen);
        
        /// <summary>
        /// Generates random bytes
        /// </summary>
        /// <param name="length">Length</param>
        /// <returns>Random bytearray</returns>
        byte[] GenerateRandomBytes(int length);
    }
}