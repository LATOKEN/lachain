using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.RPC;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Newtonsoft.Json;

namespace Lachain.Console
{
    public class TrustedKeygen
    {
        internal class Config
        {
            public Config(NetworkConfig network, GenesisConfig genesis, RpcConfig rpc, VaultConfig vault,
                StorageConfig storage, BlockchainConfig blockchain)
            {
                Network = network;
                Genesis = genesis;
                Rpc = rpc;
                Vault = vault;
                Storage = storage;
                Blockchain = blockchain;
            }

            [JsonProperty("network")] public NetworkConfig Network { get; set; }
            [JsonProperty("genesis")] public GenesisConfig Genesis { get; set; }
            [JsonProperty("rpc")] public RpcConfig Rpc { get; set; }
            [JsonProperty("vault")] public VaultConfig Vault { get; set; }
            [JsonProperty("storage")] public StorageConfig Storage { get; set; }
            [JsonProperty("blockchain")] public BlockchainConfig Blockchain { get; set; }
        }

        public static void DoKeygen(int n, int f, IEnumerable<string> ips)
        {
            if (n <= 3 * f) throw new Exception("N must be >= 3 * F + 1");
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();

            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToHex())
                .ToArray();

            var ecdsaPrivateKeys = new string[n];
            var ecdsaPublicKeys = new string[n];
            var addresses = new string[n];
            var crypto = CryptoProvider.GetCrypto();
            for (var i = 0; i < n; ++i)
            {
                ecdsaPrivateKeys[i] = crypto.GenerateRandomBytes(32).ToHex(false);
                ecdsaPublicKeys[i] = crypto.ComputePublicKey(ecdsaPrivateKeys[i].HexToBytes(), true).ToHex(false);
                addresses[i] = ecdsaPrivateKeys[i].HexToBytes().ToPrivateKey().GetPublicKey().GetAddress().ToHex();
            }

            var hubPublicKeys = new[]
            {
                "QmX6QkkMPaiXzYgpfohxuM5YGXsUgUxLzm6QTcvGhaEAxH",
                "QmYopFiwba671RGPtPEDnw8VFoS6ezrfV4Z6xHYpHP2HFC",
                "QmddajBBsydxFYkknE7uA9e6n73M1CTuqLBi5KV6XEz5zu",
                "QmXvef5fkR4etvJdXmSRHmcpZ94ZzvZMUX6LCLe4rgMBHK",
                "QmWd9EJxtEmKLwDpycTk7gxBp6to86i8XEDubNz1GcFn4n",
                "Qmb2RmhHt14cae4PDUhFaZk78x4AJPph6hEKqgUeXdChUq",
                "QmT3mNwXobUoD4HPss6xaNG8jMWknkVzztBX3DH6uuSpmc",
                "QmRgDX9xV8tkXx37vogE7QwDsPyhzJj1r3YUNcqmUdvyqj",
                "QmZChM8sPn6M27JxmivTuSrAPtb29ih72m7q3T5TjTVdsE",
                "QmQpSV3hCr4etSjq3LpyJd6PbtJCYVLDq4DD1Vd5szj23A",
                "QmeizP8WQCbGDV5s598s9fpJvLArS6qimUYWhE118nczjN",
                "QmY9vwCv6ZZfF1uwdSDECaZMU2utu712fN5viyvui71F8U",
                "QmfQHQQTxkdD5hmB5H4oaSYJqeLLkUL8UkE5hhyo1kysPq",
                "QmbTyjfofnqC4TSyXPVWe1NbABAAbBmb32drUodjtSPCn4",
                "QmT4FpAz9L3B2AL9vCiqwJSHEnLoD5xuQuW77XatgDtL8e",
                "QmbnXHVnXmYaF6tff1dGSfSSGJnWxWQW8Tyj9J3qY76pgN",
                "QmWR7GzsSZRQWYg1o3dXPiQ15Y8TzUb39uUegjjUWK56QB",
                "QmcPccDKAHtMDyCj4631H1z6EYmKYvA9cLqDTrkoZK9Z6L",
                "QmTkKYGyhR3D3GrBkjutjn1obobFfXD3SPizHSjZeiEp7g",
                "Qmag5vZe7adknhfft6JwiCSDrHhhwEup8iCks5i59KJqh1",
                "QmXACTXsDFW2LxNDWQYR1PcQ5WsWq1Ra3Zd76osLWhE8vZ",
                "QmctYjJqodqNFUynRStocsUQHpZEap8quZSEp2Ps5Kewe1"
            }.Take(n).ToArray();

            var serializedHubPrivateKeys = new[]
            {
                "0803127930770201010420418b37d00747c53fe156f3dd11fb1c80fb7af0788731ed62fea37391b459ab19a00a06082a8648ce3d030107a14403420004c74df2fd6c159e4e3eaa6ca37e35ffc9e49b49e2669e755e0a1f5c5e47450821ce2a2901679d63ef362acbc856265b279f3c19d9031897bdc17b75af762f34de",
                "0803127930770201010420e7adc0507b5b58cb6d88e8913557ddce482805fe28e1773cf5cb59e1ae80d770a00a06082a8648ce3d030107a14403420004a17a4870011e66f52ea12b0557a87e04c95a254304b3d3295553fafd0f0fdbe2cba48dd3d1b6b98198a5c04e0e3dbf28729c9eb35d37871051fbed690ab2ca2c",
                "0803127930770201010420eefe186ce798c03cdc66bd7ff6550942cd1f62eda122bebb6e567d88b6492f24a00a06082a8648ce3d030107a14403420004bb7ae6121be03a7aaebdc6c92d07ee7f23480a08d7b7e00789550a2f5ec56f7184c93a3dc95421025132e93755e74f47691c730f407cb6abd28f43beb63ccb37",
                "0803127930770201010420c3c3a30b6ff9d29f1531ba4329d46b4c6d395c3539cd8d43e7193b6f638f5c3da00a06082a8648ce3d030107a14403420004103c492c74152a88d9128774f5b1f36ea1bed23d0b3099e35922392dda7cd8fa9d64377328d17a5b4c97eabfa17b5e19cbcbb3e8d9f3be05d6600df718cd5c60",
                "08031279307702010104204a89e4a08fa2f23cd05687d55661e3a1b686bd798e36d7a0c9c5d04d042dbc86a00a06082a8648ce3d030107a144034200046babe55e4685c8da6426cd44814eb66abb1c0e3e4e70fe3ed889c7b2ed76782748cf70e8f2f7e9567e736819db4ce685456dade7b7169090e2437e89f52e0857",
                "080312793077020101042067361b9ecaa2e06c89dcec4b69df6937c7cbad6ea4384342643fa6423c2df6cea00a06082a8648ce3d030107a144034200047ecd71b6bdbc9c92a1f9b842ac73a7b4156cbb4c479038d658595b8dabbaf0541cc5742a2e5a06fab0088ffa1cbf47d0fb4a14ff1f810bdd9374a5ffc5ba1310",
                "08031279307702010104205544b32738628ccf8d8f6ceb4e85d26e3c4e5ec69309a2e3c49da80333c75ab7a00a06082a8648ce3d030107a1440342000405cff2268115e9d7e1c38983d7cc148d1c0ba98df3ebf964013229ccd5a8208e95ddd43e681d1f32502353f6580247cb32c95b09dc4fe8812647469a428441fd",
                "0803127930770201010420582a8f07e4d860cba226b5933ab13d1a7caa8d3567c290e2bf1ea68e01106effa00a06082a8648ce3d030107a14403420004dd5fec49c8c85d078ed7a7724aaae01a2e6a0a0cb92f4812c588810edd5b3b76bae33e8f9c2b21cc303151009dd4ab7fcff26df0b447464a1588d06b16ea4510",
                "080312793077020101042023d4a96bc8db3eb24bde617716ffa27d57255ed12438f4a1fec1c827d754e3eba00a06082a8648ce3d030107a14403420004c55a56c9d33ee472e93c80645699162df7e56903621a046f0b271cf7d0313d419e8f6960acf8af604afeec19804433ba022a23ec8f23b5d42f91505ca1d87cbf",
                "0803127930770201010420cdb5adc2b790408b9f4fa0d1217fa6cd4d256f69f2a71c26b554c92bf544a8f7a00a06082a8648ce3d030107a14403420004a9ba44b8c862ac9043ac072fe6fa99b6561dc179527a4493098985380eaf45b7a3651dcba84b7b5dcd8d30bdf9a6a22a5a49e7202feb881060c0e173e0d7e4d6",
                "08031279307702010104208b68214458fada336e3f7ade2613cb4f5ac9d2cea6631d363d17c0dd685bb61aa00a06082a8648ce3d030107a14403420004f2b00518b454d8d51dad79c73124ce90584c15e0da954fff6b49fdb727e2f186b690fe0ca6d9ebd71668f2de285d78e9fa613367c7750546b82ffe544fae143c",
                "0803127930770201010420c3c981bf5228f8fb31467ed0d43f12cd34067422723baa432e42310bca35ff3da00a06082a8648ce3d030107a14403420004fb502eb1ccba26ea6dbe070c042c10d85607cc8652962c3ceeaa433f5ca454e5722ff20ad322dd1cb9d0563484d554a7c6095d32b46b5e70f8753013f666da7a",
                "0803127930770201010420d7daa95c7ca573dfa21419c4fa199e9cbadf9e59cbf0cd4ab4923bee8455e34ca00a06082a8648ce3d030107a14403420004e6f467a7f9afe16c91b039bf3921f47d456a504400cb0fe670ccab58fe85450e423c41cec4659158adf3685496034902aaa887a30982e387f32047c7af7f9e38",
                "08031279307702010104203311d9849154607d7b12d40ef5e77bef2605d44f8296fae9f476344ceaf5835fa00a06082a8648ce3d030107a14403420004da1e33a0ec298f6c34995b6577ef504a2d29cb22c0e26f5d95002960fc5877e85345c6e414608a19502a9c231b2de63e9e8f2c172bb5cce4289471acf20caf21",
                "0803127930770201010420aa8868c99164a79cb6216952c95905010b76af8137e1bbab5f0457ebd6cfb6d1a00a06082a8648ce3d030107a14403420004a98f0b9cd12a71a9a96b47bb7da1eca0e17e6461b688a8d0214b852c17d74d2e562a788d2259349a254ea54421d8a39b95a749fedbd3f4c30a3b1693df347cfa",
                "0803127930770201010420e046b7cc77e48ad3a47a5d6d0414dbd94d29ceb6cb4e7fa639b1a70b7a28daafa00a06082a8648ce3d030107a1440342000429da7464d71b4558dd33a5352572e22396dbcf9246bc9b73839630fc6814fbe345872532c0fdbdec0cdec3ec6ea260c11b128f79cb734568de5f3af1eba5263f",
                "080312793077020101042009d6b407e1a69e764bc100252406d01f70b2371879efa8cedbde4da76dfb56f1a00a06082a8648ce3d030107a1440342000449f3915ef32f3503a5d79bc80682eb213901a6d3534219267d784b1ab0ddeb45792a2479a10838a67ef9b9b218c7e54f759981802a880648688f4f03815dcbac",
                "0803127930770201010420115dcea23199c00d710d5bd375593ac3b8eb2585a2a72befdae4132db187c65ea00a06082a8648ce3d030107a14403420004a03cce2bff974361abccb5a23b6ff02d3ae187927eb23044e9165bba9412d72c086a686a10fc541c834e32ef6fe9e5eff92c557dc613e7fcd3761fb29f776586",
                "0803127930770201010420c9bebb4c1e03ee71088dea55904717357d41ca6bde0f8ddd3bb0b155fda8f544a00a06082a8648ce3d030107a144034200046377dbb8eb5a6ce05f3842d53a3db7c28fb6ca834e592e8f454c0d221c69128f28bfafb0982b5a32c064763d4bcf09832ad55f180e65676a1af5b8b582981abe",
                "0803127930770201010420a4739bc1baccb8e3e0da4721bec9532ba23c052a0a2186a06eee9cb207996665a00a06082a8648ce3d030107a14403420004d79d6b2ce08d3a47084b3e4abe9f3c3e13fe61ba187c1bb021b4e9e0f1c1ce747e3792684cc2f448a67e2f3aa8089ed02838aa304503abc26fc412f049616232",
                "08031279307702010104209ca9ab6193f67c7808472e6be130955188874ac8ea646c14ed65dba7a421fa6ea00a06082a8648ce3d030107a14403420004ca2929e743ee818a2c72c0a1e1404a06d04d82904f052223234f4a31f05a29d726eec5e34f4abefe39224a4b303aa016d40f5c38a5d650c4caec72b418f72c85",
                "08031279307702010104200cff310237a0b10221160cf75901c6dc6ea880f61e1e1c99852ca5b1294217f2a00a06082a8648ce3d030107a14403420004670063fca7a03238373b4b112b8b6249d2323c7762ae095746290df502c60cb5f4891f57a84ceb5ef3af0e93238fa88cdf1cd57b3656f98b86c5fbf402615179",
            }.Take(n).ToArray();

            var bootstraps = ips
                .Zip(hubPublicKeys, (ip, id) => $"{id}@{ip}")
                .Select((x, i) => $"{x}:{41011 + i}")
                .ToArray();

            var peers = ecdsaPublicKeys.ToArray();

            for (var i = 0; i < n; ++i)
            {
                var net = new NetworkConfig
                {
                    Peers = peers,
                    MaxPeers = 100,
                    ForceIPv6 = false,
                    BootstrapAddresses = bootstraps
                };
                var genesis = new GenesisConfig(tpkePubKey.ToHex(), "5.000000000000000000", "0.000000100000000000")
                {
                    Balances = new Dictionary<string, string>
                    {
                        {
                            "0x6bc32575acb8754886dc283c2c8ac54b1bd93195", "1000000"
                        }
                    },
                    Validators = Enumerable.Range(0, n).Select(j => new ValidatorInfo(
                        ecdsaPublicKeys[j], pubShares[j]
                    )).ToList()
                };
                for (var j = 0; j < n; ++j)
                {
                    genesis.Balances[addresses[j]] = "1000000";
                }

                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = 7070,
                    ApiKey = "asdasdasd",
                };
                var walletPath = "wallet.json";
                var vault = new VaultConfig
                {
                    Path = walletPath,
                    Password = "12345"
                };
                var storage = new StorageConfig
                {
                    Path = "ChainLachain",
                    Provider = "RocksDB",
                };
                var blockchain = new BlockchainConfig();
                var config = new Config(net, genesis, rpc, vault, storage, blockchain);
                File.WriteAllText($"config{i + 1:D2}.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                File.WriteAllText($"prv_h{i + 1}.txt", serializedHubPrivateKeys[i]);
                GenWallet(
                    $"wallet{i + 1:D2}.json",
                    ecdsaPrivateKeys[i],
                    tpkeKeyGen.GetPrivKey(i).ToHex(),
                    privShares[i].ToHex()
                );
            }

            var tpkePrivKeys = string.Join(
                ", ",
                Enumerable.Range(0, n)
                    .Select(idx => tpkeKeyGen.GetPrivKey(idx))
                    .Select(x => $"\"{x.ToHex()}\""));
            var tsKeys = string.Join(
                ", ",
                sigKeyGen.GetPrivateShares()
                    .Select(x => $"(\"{x.GetPublicKeyShare().ToHex()}\", \"{x.ToHex()}\")")
            );
            System.Console.WriteLine(
                $"{n}: " + "{" +
                "  \"tpke\": (" +
                $"    \"{tpkePubKey.ToHex()}\"," +
                $"    [{tpkePrivKeys}]" +
                "  )," +
                $"  \"ts\": [{tsKeys}]," +
                "}");
            System.Console.WriteLine(
                string.Join(", ", ecdsaPrivateKeys.Zip(ecdsaPublicKeys).Zip(addresses)
                    .Select(t => $"(\"{t.First.Second}\", \"{t.First.First}\", \"{t.Second}\")"))
            );
        }

        private static void GenWallet(string path, string ecdsaKey, string tpkeKey, string tsKey)
        {
            var config = new JsonWallet(
                ecdsaKey,
                new Dictionary<ulong, string> {{0, tpkeKey}},
                new Dictionary<ulong, string> {{0, tsKey}}
            );
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            var passwordHash = Encoding.UTF8.GetBytes("12345").KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            File.WriteAllBytes(path, crypto.AesGcmEncrypt(passwordHash, json));
        }
    }
}