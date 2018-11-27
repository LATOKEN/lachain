using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.Utils;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public class ValidatorManager : IValidatorManager
    {
        public ValidatorManager(IConfigManager configManager)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            Validators = config.ValidatorsKeys.Select(key => key.HexToBytes().ToPublicKey()).ToList();
        }

        public IReadOnlyCollection<PublicKey> Validators { get; }

        public uint Quorum => (uint) (Validators.Count * 2 / 3);
    }
}