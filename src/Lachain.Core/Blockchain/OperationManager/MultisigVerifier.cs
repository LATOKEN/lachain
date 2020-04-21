using System;
using System.Linq;
using Lachain.Logger;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class MultisigVerifier : IMultisigVerifier
    {
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private readonly ILogger<MultisigVerifier> _logger = LoggerFactory.GetLoggerForClass<MultisigVerifier>();

        public OperatingError VerifyMultisig(MultiSig multisig, UInt256 hash)
        {
            /* don't allow null multisig or hash */
            if (multisig is null || hash is null)
                return OperatingError.InvalidMultisig;
            /* check that all signatures are unique */
            if (multisig.Signatures.Select(sig => sig.Key).Distinct().Count() != multisig.Signatures.Count)
                return OperatingError.InvalidMultisig;
            /* check count of unique validators */
            if (multisig.Validators.Distinct().Count() != multisig.Validators.Count)
                return OperatingError.InvalidMultisig;
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
                    if (!_crypto.VerifySignature(hash.ToBytes(), sig, publicKey))
                        continue;
                    /* increment count of verified signatures */
                    ++verified;
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            /* TODO: "don't forget to enable this validation" */
            /*if ((int) multisig.Quorum < (int) _validatorManager.Quorum)
                return OperatingError.InvalidMultisig;*/
            /* if we have required amount of signatures that return ok */
            return verified >= multisig.Quorum ? OperatingError.Ok : OperatingError.QuorumNotReached;
        }
    }
}