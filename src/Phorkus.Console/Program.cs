using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.Console
{
    class Program
    {
        static void TrustedKeyGen()
        {
            const int n = 1, f = 0;
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();
            var tpkeVerificationKey = tpkeKeyGen.GetVerificationKey();

            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = string.Join(',', sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToByteArray().ToHex())
                .Select(s => '"' + s + '"'));

            for (var i = 0; i < n; ++i)
            {
                System.Console.WriteLine($"Player {i} config:");
                System.Console.WriteLine($"    \"TPKEPublicKey\": \"{tpkePubKey.ToByteArray().ToHex()}\",");
                System.Console.WriteLine(
                    $"    \"TPKEPrivateKey\": \"{tpkeKeyGen.GetPrivKey(i).ToByteArray().ToHex()}\",");
                System.Console.WriteLine(
                    $"    \"TPKEVerificationKey\": \"{tpkeVerificationKey.ToByteArray().ToHex()}\",");
                System.Console.WriteLine($"    \"ThresholdSignaturePublicKeys\": [{pubShares}],");
                System.Console.WriteLine($"    \"ThresholdSignaturePrivateKey\": \"{privShares[i].ToByteArray().ToHex()}\"");
                System.Console.WriteLine();
            }
        }

        internal static void Main(string[] args)
        {
            Mcl.Init();
            var app = new Application();
            app.Start(args);
        }
    }
}