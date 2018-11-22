using NeoSharp.Core.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoSharp.Core.Wallet.NEP6
{
    public class Nep6Wallet : IWallet
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("scrypt")]
        public ScryptParameters Scrypt { get; set; }
        
        [JsonConverter(typeof(Nep6AccountConverter))]
        [JsonProperty("accounts")]
        public IWalletAccount[] Accounts { get; set; }

        [JsonProperty("extra")]
        public JObject Extra { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Nep6Wallet()
        {
            Scrypt = ScryptParameters.Default;
            Accounts = new IWalletAccount[0];
        }
    }
}