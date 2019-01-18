using System;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Phorkus.Party.Signer
{
    public class Zkpi2
    {
        private CurveParams curveParams;
        private ECPoint u1;
        private BigInteger u2;
        private BigInteger u3;
        private BigInteger z1;
        private BigInteger z2;
        private BigInteger s1;
        private BigInteger s2;
        private BigInteger t1;
        private BigInteger t2;
        private BigInteger t3;
        private BigInteger e;
        private BigInteger v1;
        private BigInteger v3;

        public string serialize()
        {
            byte[] u1s = u1.GetEncoded(false);
            StringBuilder res = new StringBuilder();
            res.Append(curveParams.Name);
            res.Append("," + HexUtil.bytesToHex(u1s));
            res.Append("," + u2);
            res.Append("," + u3);
            res.Append("," + z1);
            res.Append("," + z2);
            res.Append("," + s1);
            res.Append("," + s2);
            res.Append("," + t1);
            res.Append("," + t2);
            res.Append("," + t3);
            res.Append("," + e);
            res.Append("," + v1);
            res.Append("," + v3);
            return res.ToString();
        }

        public void deserialize(String s)
        {
            string[] arr = s.Split(',');
            var curveName = arr[0];
            curveParams = new CurveParams(curveName);
            u1 = curveParams.Curve.Curve.DecodePoint(HexUtil.hexToBytes(arr[1]));
            u2 = new BigInteger(arr[2]);
            u3 = new BigInteger(arr[3]);
            z1 = new BigInteger(arr[4]);
            z2 = new BigInteger(arr[5]);
            s1 = new BigInteger(arr[6]);
            s2 = new BigInteger(arr[7]);
            t1 = new BigInteger(arr[8]);
            t2 = new BigInteger(arr[9]);
            t3 = new BigInteger(arr[10]);
            e = new BigInteger(arr[11]);
            v1 = new BigInteger(arr[12]);
            v3 = new BigInteger(arr[13]);
        }

        public Zkpi2()
        {
        }
        
        public Zkpi2(CurveParams curveParams, PublicParameters parameters, BigInteger eta1, BigInteger eta2,
            Random rand, ECPoint c, BigInteger w, BigInteger u,
            BigInteger randomness)
        {
            this.curveParams = curveParams;
            
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
            BigInteger delta = Util.randomFromZn(q.Pow(3), rand);
            BigInteger mu = Util.randomFromZnStar(N, rand);
            BigInteger nu = Util.randomFromZn(q.Pow(3).Multiply(nTilde), rand);
            BigInteger theta = Util.randomFromZn(q.Pow(8), rand);
            BigInteger tau = Util.randomFromZn(q.Pow(8).Multiply(nTilde), rand);

            BigInteger rho1 = Util.randomFromZn(q.Multiply(nTilde), rand);
            BigInteger rho2 = Util.randomFromZn(q.Pow(6).Multiply(nTilde), rand);

            z1 = h1.ModPow(eta1, nTilde).Multiply(h2.ModPow(rho1, nTilde)).Mod(nTilde);
            z2 = h1.ModPow(eta2, nTilde).Multiply(h2.ModPow(rho2, nTilde)).Mod(nTilde);
            u1 = c.Multiply(alpha);
            u2 = g.ModPow(alpha, nSquared).Multiply(beta.ModPow(N, nSquared)).Mod(nSquared);
            u3 = h1.ModPow(alpha, nTilde).Multiply(h2.ModPow(gamma, nTilde)).Mod(nTilde);
            v1 = u.ModPow(alpha, nSquared)
                .Multiply(g.ModPow(q.Multiply(theta), nSquared))
                .Multiply(mu.ModPow(N, nSquared)).Mod(nSquared);
            v3 = h1.ModPow(theta, nTilde).Multiply(h2.ModPow(tau, nTilde)).Mod(nTilde);

            byte[] digest = Util.sha256Hash(new[]
            {
                Util.getBytes(c), Util.getBytes(w), Util.getBytes(u),
                Util.getBytes(z1), Util.getBytes(z2), Util.getBytes(u1), Util.getBytes(u2),
                Util.getBytes(u3), Util.getBytes(v1), Util.getBytes(v3)
            });

            if (digest == null)
            {
                throw new Exception("Unable to calculate SHA256");
            }

            e = new BigInteger(1, digest);

            s1 = e.Multiply(eta1).Add(alpha);
            s2 = e.Multiply(rho1).Add(gamma);
            t1 = randomness.ModPow(e, N).Multiply(mu).Mod(N);
            t2 = e.Multiply(eta2).Add(theta);
            t3 = e.Multiply(rho2).Add(tau);
        }
        
        public bool verify(CurveParams curveParams, PublicParameters parameters, ECDomainParameters CURVE,
            ECPoint r, BigInteger u, BigInteger w)
        {
            ECPoint c = parameters.getG();

            BigInteger h1 = parameters.h1;
            BigInteger h2 = parameters.h2;
            BigInteger N = parameters.paillierPubKey.getN();
            BigInteger nTilde = parameters.nTilde;
            BigInteger nSquared = N.Multiply(N);
            BigInteger g = N.Add(BigInteger.One);
            BigInteger q = curveParams.Q;
            
            if (!u1.Equals(c.Multiply(s1).Add(r.Multiply(e.Negate()))))
            {
                return false;
            }

            if (!u3.Equals(h1.ModPow(s1, nTilde).Multiply(h2.ModPow(s2, nTilde))
                .Multiply(z1.ModPow(e.Negate(), nTilde)).Mod(nTilde)))
            {
                return false;
            }

            // VERIFY U2!!!
            
            if (!v1.Equals(u.ModPow(s1, nSquared)
                .Multiply(g.ModPow(q.Multiply(t2), nSquared))
                .Multiply(t1.ModPow(N, nSquared))
                .Multiply(w.ModPow(e.Negate(), nSquared)).Mod(nSquared)))
            {
                return false;
            }

            if (!v3.Equals(h1.ModPow(t2, nTilde).Multiply(h2.ModPow(t3, nTilde))
                .Multiply(z2.ModPow(e.Negate(), nTilde)).Mod(nTilde)))
            {
                return false;
            }
            
            byte[] digestRecovered = Util.sha256Hash(new[]
            {
                Util.getBytes(c), Util.getBytes(w),
                Util.getBytes(u), Util.getBytes(z1), Util.getBytes(z2), Util.getBytes(u1),
                Util.getBytes(u2), Util.getBytes(u3), Util.getBytes(v1),
                Util.getBytes(v3)
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
    }
}