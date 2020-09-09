using System;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using Lachain.Core.Blockchain;
using Newtonsoft.Json;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.CLI;
using Lachain.Core.RPC;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Storage;

namespace Lachain.Console
{
    class Program
    {
        internal static void Main(string[] args)
        {
            var x = Parser.Default
                .ParseArguments<VersionOptions, RunOptions, DecryptOptions, EncryptOptions, KeygenOptions>(args)
                .WithParsed<VersionOptions>(options => PrintVersion())
                .WithParsed<RunOptions>(RunNode)
                .WithParsed<DecryptOptions>(DecryptWallet)
                .WithParsed<EncryptOptions>(EncryptWallet)
                .WithParsed<KeygenOptions>(RunKeygen)
                .WithNotParsed(errors =>
                {
                    foreach (var error in errors)
                    {
                        System.Console.Error.WriteLine(error);
                    }
                });

            // GenWallet(
            //     "wallet.json", 
            //     "d95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48", 
            //     "0x000000000000000000000000000000000000000000000000000000000000000000000000",
            //     "0xcb436d851f7d58773a36daf94350f25635b06fb970dc670059529f6b3797b668"
            // );
            // GenWallet(
            //     "wallet0.json", 
            //     "0a63d1202aa7b5052e2a823eb3873ee9f53709e967778bf39efcf1ea05bb3907", 
            //     "0x000000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xbc055e0f7bc72bdf9b0129f85c925eff36c4b8da5a6235a4b33b2e1131c24c20"
            // );
            // GenWallet(
            //     "wallet1.json", 
            //     "7125553f3ffbaa1a0e6b8787f1ad060e201adb338a28e838f93fb06f6b7fc5de", 
            //     "0x010000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xd12e210c67814de8aed0fe42ed7ce846803f7fcfeb163ab4c38aa7bd147bf823"
            // );
            // GenWallet(
            //     "wallet2.json", 
            //     "49ace3986253b6e0300ca1fe486ece278d557e896b1f7f446d498586913b9bf4", 
            //     "0x020000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xe657e408533b6ff1c19fd48d7d67728ec9ba45c47ccb3ec4d3d9206af833a427"
            // );
            // GenWallet(
            //     "wallet3.json", 
            //     "68fcd0c7ee8ca176ef46dd2c2296f20ae17e1046dd36a7a8ff25c0f9bde6002a", 
            //     "0x030000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xfb80a7053ff590fad46eaad80d52fcd512360cb90d8043d4e3289a16dcec4f2b"
            // );
        }

        private static void RunKeygen(KeygenOptions options)
        {
            // var ips = new[]
            // {
            //     "116.203.75.72", "78.46.123.99", "95.217.4.100", "88.99.190.27", "78.46.229.200", "95.217.6.171",
            //     "88.99.190.191", "94.130.78.183", "94.130.24.163", "94.130.110.127", "94.130.110.95", "94.130.58.63",
            //     "88.99.86.166", "88.198.78.106", "88.198.78.141", "88.99.126.144", "88.99.87.58", "95.217.6.234",
            //     "95.217.12.226", "95.217.14.117", "95.217.17.248", "95.217.12.230"
            // };
            var ips = options.IpAddresses.ToArray();

            if (ips.Length == 0)
                ips = new[]
                {
                    "116.203.75.72", "178.128.113.97", "165.227.45.119", "206.189.137.112", "157.245.160.201",
                    "95.217.6.171", "88.99.190.191", "94.130.78.183", "94.130.24.163", "94.130.110.127",
                    "94.130.110.95",
                    "94.130.58.63", "88.99.86.166", "88.198.78.106", "88.198.78.141", "88.99.126.144", "88.99.87.58",
                    "95.217.6.234", "95.217.12.226", "95.217.14.117", "95.217.17.248", "95.217.12.230"
                };
            TrustedKeygen.DoKeygen(ips.Length, options.F, ips);
        }

        private static void EncryptWallet(EncryptOptions options)
        {
            string path = options.WalletPath;
            path = Path.IsPathRooted(path) || path.StartsWith("~/")
                ? path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(path)), path);
            var content = File.ReadAllBytes(path) ?? throw new Exception($"Cannot read file {path}");
            var key = Encoding.UTF8.GetBytes(options.WalletPassword).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            var encryptedContent = crypto.AesGcmEncrypt(key, content);
            using var stream = System.Console.OpenStandardOutput();
            stream.Write(encryptedContent, 0, encryptedContent.Length);
        }

        private static void DecryptWallet(DecryptOptions options)
        {
            string path = options.WalletPath;
            path = Path.IsPathRooted(path) || path.StartsWith("~/")
                ? path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(path)), path);
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(options.WalletPassword).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            var decryptedContent =
                Encoding.UTF8.GetString(crypto.AesGcmDecrypt(key, encryptedContent));
            System.Console.WriteLine(decryptedContent);
        }

        private static void RunNode(RunOptions options)
        {
            using var app = new Application(options.ConfigPath, options);
            app.Start(options);
        }

        private static void PrintVersion()
        {
            System.Console.WriteLine(new NodeService(null!, null!, null!).GetNetVersion());
        }
    }
}