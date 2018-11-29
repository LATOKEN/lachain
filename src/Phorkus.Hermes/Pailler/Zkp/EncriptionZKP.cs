using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Pailler.Key;

namespace Phorkus.Hermes.Pailler.Zkp
{
public class EncryptionZKP : ZKP {
    private static long serialVersionUID = 6683900023178557008L;
    private BigInteger nSPlusOne;
    private BigInteger n;
    private BigInteger z;
    private BigInteger w;
    private BigInteger b;
    private BigInteger c;

    public EncryptionZKP(byte[] b) {
        try {
            int offset = 0;
            int size = ByteUtils.getInt(b, offset);
            offset = offset + 4;
            this.c = ByteUtils.getBigInt(b, offset, size);
            offset += size;
            size = ByteUtils.getInt(b, offset);
            offset += 4;
            this.b = ByteUtils.getBigInt(b, offset, size);
            offset += size;
            size = ByteUtils.getInt(b, offset);
            offset += 4;
            this.w = ByteUtils.getBigInt(b, offset, size);
            offset += size;
            size = ByteUtils.getInt(b, offset);
            offset += 4;
            this.z = ByteUtils.getBigInt(b, offset, size);
            offset += size;
            size = ByteUtils.getInt(b, offset);
            offset += 4;
            this.nSPlusOne = ByteUtils.getBigInt(b, offset, size);
            offset += size;
            size = ByteUtils.getInt(b, offset);
            offset += 4;
            this.n = ByteUtils.getBigInt(b, offset, size);
            int var10000 = offset + size;
        } catch (IndexOutOfRangeException var4) {
            throw new ArgumentException("byte input corrupted or incomplete");
        }
    }

    public EncryptionZKP(byte[] b, BigInteger nSPlusOne, BigInteger n)
        :this(ByteUtils.appendBigInt(b, new BigInteger[]{nSPlusOne, n})){ }

    public EncryptionZKP(byte[] b, PaillierKey pubkey)
        :this(b, pubkey.getNSPlusOne(), pubkey.getN()) { }

    public EncryptionZKP(PaillierKey key, BigInteger alpha) {
        if (!key.inModN(alpha)) {
            throw new ArgumentException("alpha must be 0 <= alpha < n");
        } else {
            BigInteger c = null;
            BigInteger s = null;
            BigInteger x = null;
            BigInteger u = null;
            BigInteger b = null;
            BigInteger e = null;
            BigInteger w = null;
            BigInteger t = null;
            BigInteger z = null;
            BigInteger dummy = null;
            BigInteger n = key.getN();
            BigInteger nPlusOne = key.getNPlusOne();
            BigInteger nSquare = key.getNSPlusOne();
            s = key.getRandomModNStar();
            c = AbstractPaillier.encrypt(alpha, s, key);
            x = key.getRandomModN();
            u = key.getRandomModNSPlusOneStar();
            b = nPlusOne.ModPow(x, nSquare).Multiply(u.ModPow(n, nSquare)).Mod(nSquare);
            
            //e = this.hash(new byte[][] {c.ToByteArray(), b.ToByteArray()});
            
            
            
            var bytes = new[] { c.ToByteArray(), c.ToByteArray() };
            
            e = this.hash(bytes);
            
            dummy = x.Add(e.Multiply(alpha));
            w = dummy.Mod(n);
            t = dummy.Divide(n);
            z = u.Multiply(s.ModPow(e, nSquare)).Multiply(nPlusOne.ModPow(t, nSquare)).Mod(nSquare);
            this.c = c;
            this.b = b;
            this.w = w;
            this.z = z;
            this.n = key.getN();
            this.nSPlusOne = key.getNSPlusOne();
        }
    }

    public override BigInteger getValue() {
        return c;
    }

    public override bool Verify() {
        BigInteger nPlusOne = n.Add(BigInteger.One);
        BigInteger e = this.hash(new byte[][] {c.ToByteArray(), b.ToByteArray()});

        try {
            return nPlusOne.ModPow(w, nSPlusOne).Multiply(z.ModPow(n, nSPlusOne)).Mod(nSPlusOne).CompareTo(b.Multiply(c.ModPow(e, nSPlusOne)).Mod(this.nSPlusOne)) == 0;
        } catch (ArithmeticException var4) {
            return false;
        }
    }

    public bool verifyKey(PaillierKey origkey) {
        return nSPlusOne.Equals(origkey.getNSPlusOne()) && n.Equals(origkey.getN());
    }

    public override byte[] toByteArray() {
        return ByteUtils.appendBigInt(toByteArrayNoKey(), new BigInteger[] {nSPlusOne, n});
    }

    public override byte[] toByteArrayNoKey() {
        byte[] p = this.c.ToByteArray();
        byte[] r = new byte[p.Length + 4];
        Array.Copy(ByteUtils.intToByte(p.Length), 0, r, 0, 4);
        Array.Copy(p, 0, r, 4, p.Length);
        return ByteUtils.appendBigInt(r, new BigInteger[]{b, w, z});
    }
}

}