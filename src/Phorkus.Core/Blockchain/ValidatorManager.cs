using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class ValidatorManager : IValidatorManager
    {
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        public ValidatorManager(IConfigManager configManager)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            Validators = config.ValidatorsEcdsaPublicKeys
                .Select(key => key.HexToBytes())
                .OrderBy(key => key.Sha256().ToHex())
                .Select(key => key.ToPublicKey())
                .ToList();
        }

        public IReadOnlyCollection<ECDSAPublicKey> Validators { get; }

        public uint Quorum => (uint) (Validators.Count * 2 / 3);

        public ECDSAPublicKey GetPublicKey(uint validatorIndex)
        {
            return Validators.ElementAt((int) validatorIndex);
        }

        public uint GetValidatorIndex(ECDSAPublicKey publicKey)
        {
            var index = 0u;
            foreach (var validator in Validators)
            {
                if (validator.Equals(publicKey))
                    return index;
                ++index;
            }

            throw new Exception("Unable to determine validator's index");
        }

        public bool CheckValidator(UInt160 address)
        {
            foreach (var validator in Validators)
            {
                var validatorAddress = _crypto.ComputeAddress(validator.Buffer.ToByteArray());
                if (validatorAddress.Equals(address))
                    return true;
            }

            return false;
        }

        public bool CheckValidator(ECDSAPublicKey publicKey)
        {
            return Validators.Contains(publicKey);
        }
    }
}