using System.IO;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Signer;

namespace Phorkus.Hermes.Crypto.Key
{
/**
 * A private key for the threshold Paillier scheme <i>CS</i><sub>1</sub>.  This
 * key is used to partially decrypt a ciphertext. At least
 * <i>{@linkplain PaillierThresholdKey#w}</i> cooperating decryption servers 
 * are needed in this scheme to produce a full decryption.  The public
 * information provided for in 
 * <p>
 * The private key in this threshold scheme requires the following information
 * to produce a partial decryption:
 * <ul>
 *   <li>The public values <i>&Delta;</i> and <i>n</i> provided for in
 *       {@link PaillierThresholdKey}
 *   <li><i>i</i> is the decryption server ID for this particular secret key
 *   <li><i>s<sub>i</sub></i>, which is the secret share particular to this
 *       server.  This value is no other than <i>f</i>(<i>i</i>) for the
 *       (<i>w</i>-1)-degree polynomial <i>f</i> created by the key
 *       distributor.
 *   
 * </ul>
 * 
 * <p> This class provides one unique implementation of the private key in both
 * the centralized and distributed generation approach. A specific constructor
 * can be used in the case where no trusted dealer is present and the key where
 * generated using the method described by T. Nishide and K. Sakurai in 
 * <i>Distributed Paillier Cryptosystem without Trusted Dealer</i>.
 * 
 * @author James Garrity
 * @author Sean Hall
 * @version 0.9 03/25/10
 * @see PaillierKey
 * @see paillierp.key.KeyGen
 */
    public class PaillierPrivateThresholdKey : PaillierThresholdKey
    {
        /*
         * 
         * Fields
         * 
         */

        /**
         * This Serial ID
         */
        private static long serialVersionUID = 5024312630277081335L;

        /**
         * The secret share. This is unique among the <i>L</i> decryption
         * servers.
         */
        protected BigInteger si = null;

        /**
         * The server's id in the range of [1, <code>l</code>].  This identifies
         * which verification key <code>vi[id]</code> to use.
         */
        protected int id;

        /*
         * 
         * Constructors
         * 
         */

        /**
         * Creates a new private key for the generalized Paillier threshold scheme
         * from the given modulus <code>n</code>, for use on <code>l</code>
         * decryption servers, <code>w</code> of which are needed to decrypt
         * any message encrypted by using this public key.  The values
         * <code>v</code> and <code>vi</code> correspond to the public
         * values <i>v</i> and
         * <i>v<sub>i</sub></i>=<i>v</i><sup><i>l</i>!<i>s<sub>i</sub></i></sup>
         * needed to Verify the zero knowledge proofs.  {@code si} is the secret share
         * for this decryption key, and {@code i} is the ID.
         * 
         * @param n        a safe prime product of <i>p</i> and <i>q</i> where
         *                 <i>p'</i>=(<i>p</i>-1)/2 and <i>a'</i>=(<i>a</i>-1)/2
         *                 are also both primes
         * @param l        number of decryption servers
         * @param w        threshold of servers needed to successfully decrypt any
         *                 ciphertext created by this public key.  Note that
         *                 <code>w</code>&le;<code>l</code>/2.
         * @param v        a generator of a cyclic group of squares in
         *                 <i>Z</i><sup>*</sup><sub><code>n</code><sup>2</sup></sub>
         * @param viarray  array of verification keys where <code>vi[i]</code> is
         *                 <code>v</code><sup><code>l</code>!<i>s</i><sub><code>i</code></sub></sup>
         *                 where <i>s</i><sub><code>i</code></sub> is the private key
         *                 for decryption server <code>i</code>
         * @param si       secret share for this server
         * @param i        ID of the decryption server (from 1 to {@code l})
         * @param seed     a long integer needed to start a random number generator
         */
        public PaillierPrivateThresholdKey(BigInteger n, int l, int w,
            BigInteger v, BigInteger[] viarray, BigInteger si, int i, int seed)
            : base(n, l, w, v, viarray, seed)
        {
            this.si = si;
            this.id = i;
        }

        /**
         * Creates a new private key for the generalized Paillier threshold scheme
         * from the given modulus <code>n</code>, for use on <code>l</code>
         * decryption servers, <code>w</code> of which are needed to decrypt
         * any message encrypted by using this private key.  The values
         * <code>v</code> and <code>vi</code> correspond to the public
         * values <i>v</i> and
         * <i>v<sub>i</sub></i>=<i>v</i><sup><i>l</i>!<i>s<sub>i</sub></i></sup>
         * needed to Verify the zero knowledge proofs.  {@code si} is the secret share
         * for this decryption key, and {@code i} is the ID.
         * 
         * @param n        a safe prime product of <i>p</i> and <i>q</i> where
         *                 <i>p'</i>=(<i>p</i>-1)/2 and <i>a'</i>=(<i>a</i>-1)/2
         *                 are also both primes
         * @param l        number of decryption servers
         * @param combineSharesConstant
         *                 precomputed value (4<code>*l</code>!)<sup>-1</sup>
         *                 mod <code>n</code>
         * @param w        threshold of servers needed to successfully decrypt any
         *                 ciphertext created by this public key.  Note that
         *                 <code>w</code>&le;<code>l</code>/2.
         * @param v        a generator of a cyclic group of squares in
         *                 <i>Z</i><sup>*</sup><sub><code>n</code><sup>2</sup></sub>
         * @param viarray  array of verification keys where <code>vi[i]</code> is
         *                 <code>v</code><sup><code>l</code>!<i>s</i><sub><code>i</code></sub></sup>
         *                 where <i>s</i><sub><code>i</code></sub> is the private key
         *                 for decryption server <code>i</code>
         * @param si       secret share for this server
         * @param i        ID of the decryption server (from 1 to {@code l})
         * @param seed     a long integer needed to start a random number generator
         */
        public PaillierPrivateThresholdKey(BigInteger n, int l,
            BigInteger combineSharesConstant, int w, BigInteger v,
            BigInteger[] viarray, BigInteger si, int i, int seed)
            : base(n, l, combineSharesConstant, w, v, viarray, seed)
        {
            this.si = si;
            this.id = i;
        }


        /**
         * Creates a new private key for the generalized Paillier threshold scheme
         * from the given modulus <code>n</code>, for use on <code>l</code>
         * decryption servers, <code>w</code> of which are needed to decrypt
         * any message encrypted by using this private key.  The values
         * <code>v</code> and <code>vi</code> correspond to the public
         * values <i>v</i> and
         * <i>v<sub>i</sub></i>=<i>v</i><sup><i>l</i>!<i>s<sub>i</sub></i></sup>
         * needed to Verify the zero knowledge proofs.  {@code si} is the secret share
         * for this decryption key, and {@code i} is the ID.
         * <p> This constructor is meant to be used in the case of a decentralized 
         * key generation, as described by T. Nishide and K. Sakurai in 
         * <i>Distributed Paillier Cryptosystem without Trusted Dealer</i>. In this case, a
         * new public value &Theta;' needs to be part of the public key.
         * 
         * @param n        a safe prime product of <i>p</i> and <i>q</i> where
         *                 <i>p'</i>=(<i>p</i>-1)/2 and <i>a'</i>=(<i>a</i>-1)/2
         *                 are also both primes
         * @param thetaprime &Theta;' = &Delta; &Phi;(<i>N</i>) &Beta; + <i>N</i> &Delta; <i>R</i>
         * 					 in the context of Threshold Paillier without trusted dealer, where the private
         *					 keys are of the form &Theta;' - <i>N f</i>(<i>x</i>). Other constructors assume
         *					 that it is not the case and set &Theta;' = 1
         * @param l        number of decryption servers
         * @param combineSharesConstant
         *                 precomputed value (4<code>*l</code>!)<sup>-1</sup>
         *                 mod <code>n</code>
         * @param w        threshold of servers needed to successfully decrypt any
         *                 ciphertext created by this public key.  Note that
         *                 <code>w</code>&le;<code>l</code>/2.
         * @param v        a generator of a cyclic group of squares in
         *                 <i>Z</i><sup>*</sup><sub><code>n</code><sup>2</sup></sub>
         * @param viarray  array of verification keys where <code>vi[i]</code> is
         *                 <code>v</code><sup><code>l</code>!<i>s</i><sub><code>i</code></sub></sup>
         *                 where <i>s</i><sub><code>i</code></sub> is the private key
         *                 for decryption server <code>i</code>
         * @param si       secret share for this server
         * @param i        ID of the decryption server (from 1 to {@code l})
         * @param seed     a long integer needed to start a random number generator
         */
        public PaillierPrivateThresholdKey(BigInteger n,
            BigInteger thetaprime,
            int l,
            int w,
            BigInteger v,
            BigInteger[] viarray,
            BigInteger si,
            int i,
            int seed)
            : base(n, thetaprime, l, w, v, viarray, seed)
        {
            this.si = si;
            this.id = i;
        }

        public PaillierPrivateThresholdKey(byte[] b, int seed, bool old)
            : base(ByteUtils.getLowerLayer(b), seed, old)
        {
            int offset = ByteUtils.getInt(b, b.Length - 4);
            this.id = ByteUtils.getInt(b, offset);
            offset += 4;
            this.si = ByteUtils.getBigInt(b, offset + 4, ByteUtils.getInt(b, offset));
        }

        /**
         * Creates a new private key using a byte encoding of a key.
         * 
         * @param b			Byte array of the necessary values of this private key
         * @param seed		a long integer needed to start a random number generator
         * @throws IOException if the key could not be read from the byte array
         * 
         * @see #toByteArray()
         */
        public PaillierPrivateThresholdKey(byte[] b, int seed)
            : base(b, seed)
        {
            // The encoding is:
            // [ prev. layer ]
            // [ id ] 4 bytes
            // [ bitlength si ] 4 bytes
            // [ si ]

            using (var stream = new MemoryStream(b, base.byteArraySize(), b.Length))
            using (var reader = new BinaryReader(stream))
            {
                this.id = reader.ReadInt32();
                int siByteLength = reader.ReadInt32();
                byte[] siBytes = reader.ReadBytes(siByteLength);
                this.si = new BigInteger(siBytes);
            }
        }

        /*
         * 
         * Methods
         * 
         */

        /**
         * Describes if this key can be used to encrypt
         * @return     'true' if it can encrypt.
         */
        public bool canEncrypt()
        {
            return true;
        }

        /**
         * Returns the secret share key which corresponds to this
         * private key package.  This was generated and given to this
         * decryption server.
         * 
         * @return     secret share of this decryption server
         */
        public BigInteger getSi()
        {
            return si;
        }

        /**
         * Returns the id of this private key.  Mostly used to identify
         * which verification key in {@link #vi} corresponds with this
         * private key.
         * 
         * @return		ID of the decryption server's private key
         */
        public int getID()
        {
            return id;
        }

        /**
         * Encodes this key into a byte array.  As this is a public threshold key,
         * the public modulo {@code n}, {@code l}, {@code w}, {@code v}, 
         * {@code vi}, {@code id}, and {@code si} will be encoded in that order.
         * Further, before each BigInteger (except {@code n}) is the 4-byte
         * equivalent to the size of the BigInteger for later parsing.
         * 
         * @return			A byte array containing the most necessary values
         * 					of this key.  A byte array of size 0 is returned
         * 					if the key would be too large.
         * 
         * @see #PaillierPrivateThresholdKey(byte[], long)
         * @see BigInteger#toByteArray()
         */
        public byte[] toByteArray()
        {
            // The encoding is:
            // [ prev. layer ]
            // [ id ] 4 bytes
            // [ bitlength si ] 4 bytes
            // [ si ]

            byte[] upperLayer = base.toByteArray();

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(upperLayer, 0, upperLayer.Length);
                writer.Write(id);
                writer.Write(si.ToByteArray().Length);
                writer.Write(si.ToByteArray(), 0, si.ToByteArray().Length);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}