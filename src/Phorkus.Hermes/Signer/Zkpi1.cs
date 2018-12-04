using System;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer
{
    public class Zkpi1
    {
        private BigInteger z;
        private BigInteger u1;
        private BigInteger u2;
        private BigInteger s1;
        private BigInteger s2;
        private BigInteger s3;
        private BigInteger e;
        private BigInteger v;

        public Zkpi1()
        {
        }

        public Zkpi1(CurveParams curveParams, PublicParameters parameters, BigInteger eta, Random rand,
            BigInteger r, BigInteger c1, BigInteger c2, BigInteger c3)
        {
            BigInteger N = parameters.paillierPubKey.getN();
            BigInteger q = curveParams.Q;
            BigInteger nSquared = N.Multiply(N);
            BigInteger nTilde = parameters.nTilde;
            BigInteger h1 = parameters.h1;
            BigInteger h2 = parameters.h2;
            BigInteger g = N.Add(BigInteger.One);

            BigInteger alpha = Util.randomFromZn(q.Pow(3), rand);
            BigInteger beta = Util.randomFromZnStar(N, rand);
            BigInteger gamma = Util.randomFromZn(q.Pow(3).Multiply(nTilde), rand);
            BigInteger rho = Util.randomFromZn(q.Multiply(nTilde), rand);

            z = h1.ModPow(eta, nTilde).Multiply(h1.ModPow(rho, nTilde)).Mod(nTilde);
            u1 = g.ModPow(alpha, nSquared).Multiply(beta.ModPow(N, nSquared)).Mod(nSquared);
            u2 = h1.ModPow(alpha, nTilde).Multiply(h2.ModPow(gamma, nTilde)).Mod(nTilde);
            v = c2.ModPow(alpha, nSquared);

            byte[] digest = Util.sha256Hash(new[]
            {
                Util.getBytes(c1), Util.getBytes(c2), Util.getBytes(c3),
                Util.getBytes(z), Util.getBytes(u1), Util.getBytes(u2), Util.getBytes(v)
            });

            if (digest == null)
                throw new Exception("Unable to calculate SHA256 checksum");

            e = new BigInteger(1, digest);

            s1 = e.Multiply(eta).Add(alpha);
            s2 = r.ModPow(e, N).Multiply(beta).Mod(N);
            s3 = e.Multiply(rho).Add(gamma);
        }

        public bool verify(PublicParameters parameters, ECDomainParameters CURVE,
            BigInteger c1, BigInteger c2, BigInteger c3)
        {
            BigInteger h1 = parameters.h1;
            BigInteger h2 = parameters.h2;
            BigInteger N = parameters.paillierPubKey.getN();
            BigInteger nTilde = parameters.nTilde;
            BigInteger nSquared = N.Pow(2);
            BigInteger g = N.Add(BigInteger.One);

            if (!u1.Equals(g.ModPow(s1, nSquared).Multiply(s2.ModPow(N, nSquared))
                .Multiply(c3.ModPow(e.Negate(), nSquared)).Mod(nSquared)))
            {
                return false;
            }

//            if (!u2.Equals(h1.ModPow(s1, nTilde).Multiply(h2.ModPow(s3, nTilde))
//                .Multiply(z.ModPow(e.Negate(), nTilde)).Mod(nTilde)))
//            {
//                return false;
//            }

            if (!v.Equals(c2.ModPow(s1, nSquared)
                .Multiply(c1.ModPow(e.Negate(), nSquared)).Mod(nSquared)))
            {
                return false;
            }


            byte[] digestRecovered = Util.sha256Hash(new[]
            {
                Util.getBytes(c1), Util.getBytes(c2), Util.getBytes(c3),
                Util.getBytes(z), Util.getBytes(u1), Util.getBytes(u2), Util.getBytes(v)
            });

            if (digestRecovered == null)
            {
                return false;
            }

            BigInteger eRecovered = new BigInteger(1, digestRecovered);

            if (!eRecovered.Equals(e))
            {
                return false;
            }

            return true;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var str = reader.ReadString();
                var bigs = str.Split('|').Select(value => new BigInteger(value)).ToArray();
                z = bigs[0];
                u1 = bigs[1];
                u2 = bigs[2];
                s1 = bigs[3];
                s2 = bigs[4];
                s3 = bigs[5];
                e = bigs[6];
                v = bigs[7];
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var bigs = new[] {z, u1, u2, s1, s2, s3, e, v};
                var str = string.Join("|", bigs.Select(value => value.ToString()));
                writer.Write(str);
                return stream.ToArray();
            }
        }
    }
}