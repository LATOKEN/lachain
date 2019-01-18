using System;
using System.IO;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Phorkus.Party.Crypto.Key
{
/** 
 * A simple public key for the generalized Paillier cryptosystem
 * <i>CS</i><sub>1</sub>.  This public key is intended to be extended for other
 * private keys and for the Paillier Threshold scheme <b>with the degree <i>s</i>
 * to be fixed as 1.</b>
 * <p>
 * The public key for the generalized Paillier cryptosystem
 * <i>CS<sub>s</sub></i> constructed in Damg&aring;rd et al. requires an
 * <i>n</i> and <i>g</i> as defined as follows:
 * <ul>
 *   <li><i>n</i> is an admissible RSA modulus, that is, the product of two
 *       odd primes <i>p, q</i> where <i>p</i> and <i>q</i> are of equal size,
 *       and <i>p</i>&ne;<i>q</i>.  Ideally, gcd(<i>n</i>,&phi;(<i>n</i>))=1.
 *   <li><i>g</i> is an element of the multiplicative group
 *       <i>Z</i><sup>*</sup><sub><i>n</i><sup><i>s</i>+1</sup></sub> such that
 *       <i>g</i>=(1+<i>n</i>)<sup><i>j</i></sup><i>x</i> mod
 *       <i>n</i><sup><i>s</i>+1</sup> for some <i>j</i> relatively prime to
 *       <i>n</i> and <i>x</i> &isin; <i>Z</i><sup>*</sup><sub><i>n</i></sub>.
 * </ul>
 * According to the paper, the choice of <i>g</i> does not affect the semantic
 * security of the system.  Further, to fix <i>g</i> would be sufficient and
 * efficient.  Following this simplification in the paper, we will <b>choose
 * <i>g</i>=<i>n</i>+1 always</b>; hence in determining <i>g</i>, we fix
 * <i>j,x</i>=1.
 * <p>
 * In this implementation, the user only chooses the <i>n</i> as the <i>g</i>
 * is fixed.  He can supply either the two primes or <i>n</i> itself, trusting
 * that it a RSA modulus.
 * <p>
 * In addition, in this implementation the key also carries a random number
 * generator with it.  This is to facilitate the choosing of random numbers
 * modulo <i>n</i>.  The user has full freedom to change the random number
 * generator to be as secure (or insecure) as one chooses.
 * 
 * @author James Garrity
 * @author Sean Hall
 * @version 1.0 03/25/10
 */
//[Akka.Serializable]
public class PaillierKey {
	
	/*
	 * 
	 * Fields
	 * 
	 */
	
	/**
	 * This Serial ID
	 */
	private static long serialVersionUID = 6257110251194073448L;

	/** The modulus <i>n</i>, an RSA number. */
	protected BigInteger n=null;
	
	/** The cached value of <i>n<sup>s</sup></i>.*/
	protected BigInteger ns=null;
	
	/**
	 * Cached value of <i>n</i><sup><i>s</i>+1</sup>.
	 */
	protected BigInteger nSPlusOne=null;
	
	/**
	 * Cached (<i>n</i>+1) to help encryption.
	 * This is our value <i>g</i>, always set to
	 * <i>n</i>+1 for simplicity.
	 */
	protected BigInteger nPlusOne=null;
	
	/** Random number generator. */
	protected Random rnd=null;
	
	/** Bit size of n. */
	protected int k=0;
	
	/** Maximum number of bits allowed for keysize. */
	protected static int MAX_KEY_SIZE = 4096;
	
	/*
	 * 
	 * Constructors
	 * 
	 */
	
	/**
	 * Creates a new public key when given the modulus <i>n</i> and
	 * a specified random number generator.
	 * 
	 * @param n         a RSA modulus.  That is, the product of two
	 *                  different odd primes
	 * @param rnd       a specified random number generator
	 */
	public PaillierKey(BigInteger n, Random rnd) {
		if (n.BitLength > MAX_KEY_SIZE) {
			throw new ArgumentException("n must be at most "+MAX_KEY_SIZE
					+ " bits long");
		}
		this.n = n;
		//TODO Do we want to test n to make sure it is an RSA modulus?
		this.ns = n;
		this.nSPlusOne = this.n.Multiply(this.n);
		this.nPlusOne = this.n.Add(BigInteger.One);
		this.k = this.n.BitLength;
		this.rnd = rnd;
	}
	
	/** 
	 * Creates a new public key when given the modulus <i>n</i>.  This
	 * constructor will use the <code>seed</code> to create the public
	 * key with a {@link SecureRandom} random number generator.
	 * 
	 * @param n         a RSA modulus.  That is, the product of two
	 *                  different odd primes.
	 * @param seed      a long integer needed to start a random
	 *                  number generator
	 */
	public PaillierKey(BigInteger n, int seed):
		this(n, new Random(seed))
	{
	}
	
	/**
	 * Creates a new public key from a given two odd primes <i>p</i>
	 * and <i>q</i>.  This constructor will use the <code>seed</code>
	 * to create the public key with a {@link SecureRandom} random
	 * number generator.
	 * 
	 * @param p         an odd prime
	 * @param q         another odd prime different from
	 *                  <code>p</code>
	 * @param seed      a long integer needed to start a random
	 *                  number generator
	 */
	public PaillierKey(BigInteger p, BigInteger q, int seed)
		:this(p.Multiply(p), seed) {
		
		//TODO Check to see if p and q are of same length, for security purposes (?)
		if (p.CompareTo(q) == 0)
			throw new ArgumentException("p and q must be different primes");
		//TODO make this a checked exception (?)
	}

	public PaillierKey(byte[] b, int seed, bool old)
		:this(new BigInteger(b), seed)
	{
		/* TODO: "old variable for previous format" */
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
	public PaillierKey(byte[] b, int seed)
	:this(BigInteger.Zero, seed)
	{ 
		// The encoding is :
		// [ bitlength of n ]
		// [ n ]
		using (var stream = new MemoryStream(b))
		using (var reader = new BinaryReader(stream))
		{
			int byteLenN = reader.ReadInt32();
			byte[] temp = reader.ReadBytes(byteLenN);
			this.n = new BigInteger(temp);
			//TODO Do we want to test n to make sure it is an RSA modulus?
			this.ns = n;
			this.nSPlusOne = this.n.Multiply(this.n);
			this.nPlusOne = this.n.Add(BigInteger.One);
			this.k = this.n.BitLength;
		}
	}
	
	/*
	 * 
	 * Methods
	 * 
	 */

	/**
	 * Returns the simple public key.  This is particularly for those
	 * with private keys wishing to return only the public key associated
	 * with the private key.
	 * 
	 * @return          the Paillier public key corresponding to this
	 *                  key
	 */
	public PaillierKey getPublicKey() {
		return new PaillierKey(n, (int)rnd.NextDouble());
	}
	
	/**
	 * Describes if this key can be used to encrypt.  This method
	 * can be used to differentiate between a public and private
	 * key.
	 * 
	 * @return          'true' if it can encrypt
	 */
	public bool canEncrypt() {
		return false;
	}

	/**
	 * Returns the modulus <i>n</i>, which in essence <i>is</i>
	 * the public key.
	 * 
	 * @return          the RSA modulus used in this public key
	 */
	public BigInteger getN() {
		return n;
	}

	/**
	 * Returns the cached value of <i>n<sup>s</sup></i>, to be used frequently
	 * in calculations.
	 * 
	 * @return          the RSA modulus to the <i>s</i> power
	 */
	public BigInteger getNS() {
		return ns;
	}
	
	/**
	 * Returns the cached value of <i>n</i><sup><i>s</i>+1</sup>, to be used
	 * frequently in calculations.
	 * 
	 * @return          the square of the RSA modulus used in this
	 *                  key
	 */
	public BigInteger getNSPlusOne() {
		return nSPlusOne;
	}

	/**
	 * Returns the cached value of <i>n</i>+1.  This is our value
	 * <i>g</i>, chosen always to be <i>n</i>+1 for simplicity.
	 * 
	 * @return          the value <i>g</i> associate with this
	 *                  public key; fixed here to always be
	 *                  <code>n+1</code>
	 */
	public BigInteger getNPlusOne() {
		return nPlusOne;
	}

	/**
	 * Returns the random number generator used for this key.
	 * 
	 * @return          the random number generator
	 */
	public Random getRnd() {
		return rnd;
	}
	
	/**
	 * Resets the random number generator for this key, using
	 * a generated random number as the seed. 
	 */
	public void updateRnd() {
		setRnd((long)rnd.NextDouble());
	}
	
	/**
	 * Resets the random number generator for this key to be a
	 * {@link SecureRandom} random number generator, using the
	 * specified <code>seed</code> to help create it.
	 * 
	 * @param seed      a long integer needed to start a new
	 *                  random number generator
	 */
	public void setRnd(long seed) {
		setRnd(new SecureRandom(BigInteger.ValueOf(seed).ToByteArray()));
	}
	
	/**
	 * Replaces the random number generator with a user-specified
	 * generator.
	 * 
	 * @param rnd       a specified random number generator
	 */
	public void setRnd(Random rnd) {
		this.rnd = rnd;
	}
	
	/**
	 * Returns the size of <i>n</i> in bits.
	 * 
	 * @return          the size of the modulus <code>n</code>
	 */
	public int getK() {
	//TODO rename method to getSizeOfN ?
		return k;
	}
	
	/**
	 * Checks if a given number is in <i>Z</i><sub><code>n</code></sub>.
	 * 
	 * @param a         the BigInteger to be checked
	 * @param n         the BigInteger modulus
	 * @return          'true' iff <code>a</code> is non-negative
	 *                  and less than <code>n</code>
	 */
	public static bool inModN(BigInteger a, BigInteger n) {
		return (a.CompareTo(n) < 0 && a.CompareTo(BigInteger.Zero) >= 0);
	}
	
	/**
	 * Checks if a given number is in
	 * <i>Z</i><sup>*</sup><code>n</code>.
	 * 
	 * @param a         the BigInteger we are checking
	 * @return          'true' iff <code>a</code> is non-negative,
	 *                  less than <code>n</code>, and relatively
	 *                  prime to <code>n</code>
	 */
	public static bool inModNStar(BigInteger a, BigInteger n) {
		return (a.Gcd(n).Equals(BigInteger.One) && inModN(a, n));
		// Note that if a is zero, then the gcd(a,n) = gcd(0,n) = n, which is not one.
	}
	
	/**
	 * A special random number generator to find <i>r</i> in
	 * <i>Z<sub>n</sub></i>.
	 * 
	 * @return          a random integer less than <i>n</i>
	 */
	public BigInteger getRandomModN() {
		BigInteger r;
		do {
			r = new BigInteger(k,rnd);
		} while (r.CompareTo(n)>=0);
		// It is possible that a k-bit number be greater than n.
		return r;
	}
	
	/**
	 * A special random number generator to find <i>r</i> in
	 * <i>Z<sup>*</sup><sub>n</sub></i>.  In the Paillier
	 * cryptosystem, this is used to generate the random number
	 * for encryption.
	 * 
	 * @return          a random integer less than <i>n</i> and
	 *                  relatively prime to <i>n</i>
	 */
	public BigInteger getRandomModNStar() {
		BigInteger r;
		do {
			r = new BigInteger(k,rnd);
		} while (!inModNStar(r, n));
		return r;
	}

	/**
	 * A special random number generator to find <i>r</i> in
	 * <i>Z</i><sup>*</sup><sub><i>n</i><sup>2</sup></sub>.  In the Paillier
	 * cryptosystem (threshold and generalized), this is used to generate a
	 * random ciphertext for proving multiplication, decryption, and
	 * decryption.
	 * 
	 * @return          a random integer less than <i>n</i><sup>2</sup> and
	 *                  relatively prime to <i>n</i><sup>2</sup>
	 */
	public BigInteger getRandomModNSPlusOneStar() {
		BigInteger r;
		do {
			r = new BigInteger(k,rnd);
		} while (!inModNStar(r, nSPlusOne));
		return r;
	}
	
	/**
	 * Checks if a given number is in
	 * <i>Z<sup>*</sup><sub>n</sub></i>.  In the Paillier
	 * cryptosystem, the random number must be in this
	 * multiplicative group.
	 * 
	 * @param a         the BigInteger we are checking
	 * @return          'true' iff <code>a</code> is non-negative,
	 *                  less than <code>n</code>, and relatively
	 *                  prime to <code>n</code>
	 */
	public bool inModNStar(BigInteger a) {
		return inModNStar(a, n);
	}
	
	/**
	 * Checks if a given number is in
	 * <i>Z</i><sup>*</sup><sub><i>n</i><sup>2</sup></sub>.  In the Paillier
	 * cryptosystem, the ciphertext must be in this
	 * multiplicative group.
	 * 
	 * @param a         the BigInteger we are checking
	 * @return          'true' iff <code>a</code> is non-negative,
	 *                  less than <code>n</code>, and relatively
	 *                  prime to <code>n</code><sup>2</sup>
	 */
	public bool inModNSPlusOneStar(BigInteger a) {
		return inModNStar(a, nSPlusOne);
	}
	
	/**
	 * Checks if a given number is in <i>Z<sub>n</sub></i>.
	 * 
	 * @param a         the BigInteger to be checked
	 * @return          'true' iff <code>a</code> is non-negative
	 *                  and less than <code>n</code>
	 */
	public bool inModN(BigInteger a) {
		return inModN(a, n);
	}
	
	/**
	 * Checks if a given number is in <i>Z<sub>n<sup>s</sup></sub></i>.
	 * In the Paillier cryptosystem, messages to be encrypted
	 * must be in mod <i>n<sup>s</sup></i>.
	 * 
	 * @param a         the BigInteger to be checked
	 * @return          'true' iff <code>a</code> is non-negative
	 *                  and less than <code>n</code><sup><i>s</i></sup>
	 */
	public bool inModNS(BigInteger a) {
		return inModN(a, ns);
	}
	
	/**
	 * Checks to see if {@code m} is a valid plaintext, that is if it is in
	 * <i>Z<sub>n<sup>s</sup></sub></i>.
	 * 
	 * @param m         the plaintext in question
	 * @return          'true' iff {@code m} is non-negative and less than
	 *                  {@code n}<sup><i>s</i></sup>
	 */
	public bool isPlaintext(BigInteger m) {
		return inModNS(m);
	}
	
	/**
	 * Checks if a given number is in
	 * <i>Z</i><sub><i>n</i><sup>2</sup></sub>.  In the Paillier
	 * cryptosystem, ciphertext must be in mod <i>n</i><sup>2</sup>.
	 * 
	 * @param a         the BigInteger to be checked
	 * @return          'true' iff <code>a</code> is non-negative
	 *                  and less than <code>n</code>.
	 */
	public bool inModNSPlusOne(BigInteger a) {
		return inModN(a, nSPlusOne);
	}
	
	/**
	 * Checks to see if {@code c} is a valid ciphertext, that is if it is in
	 * <i>Z</i><sub><i>n</i><sup><i>s</i>+1</sup></sub>.
	 * 
	 * @param c         the ciphertext in question
	 * @return          'true' iff {@code c} is non-negative and less than
	 *                  {@code n}<sup><i>s</i>+1</sup>
	 */
	public bool isCiphertext(BigInteger c) {
		return inModNSPlusOne(c);
	}
	
	/**
	 * Encodes this key into a byte array.  As this is a public key,
	 * the public modulo {@code n} will be encoded. The size of {@code n} is
	 * not recorded.
	 * 
	 * @return			a byte array containing the most necessary values
	 * 					of this key.
	 * 
	 * @see #PaillierKey(byte[], long)
	 * @see BigInteger#toByteArray()
	 */
	public byte[] toByteArray() {
		// The encoding is :
		// [ bitlength of n ]
		// [ n ]

		using (var stream = new MemoryStream())
		using (var writer = new BinaryWriter(stream))
		{
			writer.Write(n.ToByteArray().Length);
			writer.Write(n.ToByteArray(), 0, n.ToByteArray().Length);
			writer.Flush();
			return stream.ToArray();
		}
	}

	protected int byteArraySize() {
		return 4 + n.ToByteArray().Length;
	}
}

}