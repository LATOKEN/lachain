using System.Linq;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    public class MultisigVerifier : IMultisigVerifier
    {
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private static readonly ILogger<MultisigVerifier> Logger = LoggerFactory.GetLoggerForClass<MultisigVerifier>();

        public OperatingError VerifyMultisig(MultiSig multisig, UInt256 hash, bool useNewChainId)
        {
            /* don't allow null multisig or hash */
            if (multisig is null || hash is null)
            {
                Logger.LogDebug("Multisig or hash is null");
                return OperatingError.InvalidMultisig;
            }
            /* check that all signatures are unique */
            if (multisig.Signatures.Select(sig => sig.Key).Distinct().Count() != multisig.Signatures.Count)
            {
                Logger.LogDebug("Duplicate signature in multisig");
                return OperatingError.InvalidMultisig;
            }
            /* check count of unique validators */
            if (multisig.Validators.Distinct().Count() != multisig.Validators.Count)
            {
                Logger.LogDebug("Duplicate validator in multisig");
                return OperatingError.InvalidMultisig;
            }
            /* verify every validator's signature */
            var verified = 0;
            foreach (var entry in multisig.Signatures)
            {
                /* if there is no validator's public key than skip it */
                if (!multisig.Validators.Contains(entry.Key))
                    continue;
                var publicKey = entry.Key.EncodeCompressed();
                var sig = entry.Value.Encode();
                try
                {
                    /* if signature invalid that skip it */
                    if (!_crypto.VerifySignatureHashed(hash.ToBytes(), sig, publicKey, useNewChainId))
                    {
                        Logger.LogWarning($"Invalid Multisig signature: hash: {hash.ToHex()}, sig: {sig.ToHex()}, publicKey: {publicKey.ToHex()}");
                        continue;
                    }
                    /* increment count of verified signatures */
                    ++verified;
                }
                catch (System.Exception)
                {
                    // ignore
                }
            }

            // if we have required amount of signatures that return ok
            if (verified < multisig.Quorum)
                Logger.LogWarning($"Quorum not reached: {verified} < {multisig.Quorum}");
            return verified >= multisig.Quorum ? OperatingError.Ok : OperatingError.QuorumNotReached;
        }
    }
}