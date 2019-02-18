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
        private readonly ICrypto _crypto;
        
        public ValidatorManager(
            IConfigManager configManager,
            ICrypto crypto)
        {
            _crypto = crypto;
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            Validators = config.ValidatorsKeys
                .Select(key => key.HexToBytes())
                .OrderBy(key => key.Sha256().ToHex())
                .Select(key => key.ToPublicKey())
                .ToList();
        }
        
        public IReadOnlyCollection<PublicKey> Validators { get; }

        public uint Quorum => (uint) (Validators.Count * 2 / 3);

        public PublicKey GetPublicKey(uint validatorIndex)
        {
            return Validators.ElementAt((int) validatorIndex);
        }

        public uint GetValidatorIndex(PublicKey publicKey)
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

        public bool CheckValidator(PublicKey publicKey)
        {
            return Validators.Contains(publicKey);
        }
    }
}