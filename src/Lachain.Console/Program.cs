using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Lachain.Core.CLI;
using Lachain.Crypto;
using Microsoft.Extensions.Hosting;

namespace Lachain.Console
{
    class Program
    {
        internal static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<RunOptions, DecryptOptions, EncryptOptions, KeygenOptions>(args)
                .WithParsed<RunOptions>(options => RunNode(args, options))
                .WithParsed<DecryptOptions>(DecryptWallet)
                .WithParsed<EncryptOptions>(EncryptWallet)
                .WithParsed<KeygenOptions>(RunKeygen)
                .WithNotParsed(errors =>
                {
                    foreach (var error in errors)
                    {
                        if (error is VersionRequestedError) continue;
                        System.Console.Error.WriteLine(error);
                    }
                });
        }

        private static void RunKeygen(KeygenOptions options)
        {
            TrustedKeygen.DoKeygen(options.N, options.F, options.IpAddresses);
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

        private static void RunNode(string[] args, RunOptions options)
        {
            using var app = new Application(options.ConfigPath, args, options);
            app.Start(options);
        }
    }
}