using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Pailler.Key;
using Phorkus.Hermes.Pailler.Zkp;

namespace Phorkus.Hermes.Pailler
{
/**
 * A simple implementation of the threshold Paillier encryption scheme
 * <i>CS</i><sub>1</sub>.  This is based on the scheme given in
 * <i>Generalization of Paillier's Public-Key System with Applications to
 * Electronic Voting</i> by Damg&aring;rd et al. with the parameter <b><i>s</i> 
 * fixed at 1.</b>
 * <p>
 * With most of the methods already defined in {@link AbstractPaillier}, this
 * class provides the essential methods of encryption and decryption in the
 * threshold Paillier encryption scheme, as well as a few test/diagnostic
 * methods.
 * 
 * <h3>Threshold Paillier Encryption Scheme</h3>
 * The Paillier encryption scheme is a probabilistic asymmetric encryption
 * scheme with homomorphic properties for both addition and multiplication.  It
 * takes plaintext <i>i</i> less than <i>n</i> to compute the encryption
 * <i>E(i,r)</i> for a random <i>r</i>.  Damg&aring;rd et al. constructed an
 * extension of their {@linkplain Paillier generalized scheme} to allow
 * thresholding, a property which generates a set of <i>l</i> private keys
 * instead of one, of which <i>w</i>&ge;<i>l</i>/2 private keys must cooperate
 * in decrypting a ciphertext to generate a valid plaintext.
 * <p>
 * 
 * <b>The Math:</b> The threshold Paillier encryption scheme takes a
 * {@link paillierp.key.PaillierKey PaillierKey} <i>n</i> to encrypt a plaintext
 *  <i>i</i> in <i>Z<sub>n<sup>s</sup></sub></i> by choosing a random
 * <i>r</i>&isin;<i>Z<sub>n</sub></i><sup>*</sup> by simply computing
 * (<i>n</i>+1)<i><sup>i</sup>r<sup>n</sup></i> mod <i>n</i><sup><i>s</i>+1</sup>.
 * If decryption server <i>i</i> is given a
 * {@link paillierp.key.PaillierPrivateThresholdKey PaillierPrivateThresholdKey}
 * <i>s<sub>i</sub></i>, raising a ciphertext <i>c</i> to the power
 * 2<i>&Delta;s<sub>i</sub></i> (where &Delta;=<i>l</i>!)to produce a partial
 * decryption <i>c<sub>i</sub></i>.  Working with at least <i>w</i>-1 other 
 * decryption servers, each obtains a list of at least <i>w</i> partial
 * decryptions.  By raising partial decryption <i>c<sub>i</sub></i> to the
 * exponent 2&lambda;<sub>0,<i>i</i></sub> then multiplying each ciphertext
 * together, we arrive at the value
 * <i>c'=c</i><sup>4&Delta;<sup>2</sup><i>d</i></sup>.  This is possible because
 * the key generation used a polynomial <i>f</i> of degree <i>w</i> where
 * <i>f</i>(0) = <i>d</i>.  By using the Lagrange method for interpolating
 * values of a polynomial, we need <i>at least w</i> points to find <i>d</i>.
 * Using the method devised
 * in the paper, we can use <i>c'</i>get the original message
 * <i>m</i> mod <i>n<sup>s</sup></i>.
 * <p>
 * Note that the value <i>d</i> is kept secret in the decryption of any message.
 * Only when one has <i>w</i> secret keys <i>s<sub>i</sub></i> can one construct
 * a method to find <i>d</i>.  Otherwise, during the decryption process, <i>d</i>
 * is hidden as an exponent.  Note also that the random number
 * generator is included in the key object.  (The default is
 * {@link java.security.SecureRandom}.)
 * <p>
 * Future expansions will include support for encrypting arbitrary length
 * strings/byte arrays to avoid padding issues, and support for padding.
 * 
 * @author Murat Kantarcioglu
 * @author Sean Hall
 * @author James Garrity
 * @see AbstractPaillier
 */
public class PaillierThreshold : AbstractPaillier{

	/*
	 * Fields
	 */

	/** Private Key allowing decryption; should be same as public key. */
	protected PaillierPrivateThresholdKey deckey;

	/*
	 * 
	 * Constructors
	 * 
	 */

	/**
	 * Default constructor. This constructor can be used if there is 
	 * no need to generate public/private key pair.
	 */
	public PaillierThreshold(){ }

	/**
	 * Constructs a new encryption object which uses the specified
	 * key for encryption.
	 * 
	 * @param key  Public key used for encryption
	 */
	public PaillierThreshold(PaillierThresholdKey key) {
		
		this.key = key.getPublicKey();

		encryptMode = true;
	}

	/**
	 * Constructs a new encryption/decryption object which uses the specified
	 * key for both encryption and decryption.
	 * 
	 * @param key  Private key used for decryption and encryption
	 */
	public PaillierThreshold(PaillierPrivateThresholdKey key) {
		this.key = key.getPublicKey();
		deckey = key;
		encryptMode = true;
		decryptMode = true;
	}
	
	/**
	 * Constructs a new encryption object which uses the specified
	 * key for encryption.
	 * 
	 * @param key  Public key used for encryption
	 */
	public PaillierThreshold(PaillierKey key) {
		this.key = key;
		encryptMode = true;
	}
	
	/*
	 * 
	 * Methods
	 * 
	 */

	/**
	 * Sets the mode for this object to encrypt and will use the provided
	 * key to encrypt messages.
	 *  
	 * @param key Public key which this class will use to encrypt
	 */
	void setEncryption(PaillierKey key)
	{
		if (decryptMode==false || deckey.getN() == key.getN()){
			this.key = key;
		}
		else {
			throw new ArgumentException("Given public key does not correspond to stored private key");
		}

		// Enable the encryption mode now
		encryptMode=true;
	}

	/**
	 * Sets the mode for this object to encrypt and will use the provided
	 * key to encrypt messages.
	 *  
	 * @param key Public key which this class will use to encrypt
	 */
	void setEncryption(PaillierThresholdKey key)
	{
		if (decryptMode==false || deckey.getN() == key.getN()){
			this.key = key;
		}
		else {
			throw new ArgumentException("Given public key does not correspond to stored private key");
		}

		// Enable the encryption mode now
		encryptMode=true;
	}

	/**
	 * Sets the mode for this object to decrypt and will use the provided key
	 * to decrypt only.  (Encryption will continue to be done using the key 
	 * provided in {@link #setEncryption(PaillierKey)}.)
	 * 
	 * @param key Private key which this class will use to decrypt
	 */
	void setDecryption(PaillierPrivateThresholdKey key)
	{
		this.key = key;
		deckey = key;

		// enable the decryption mode now
		decryptMode=true;
	}

	/**
	 * Sets the mode for this object to decrypt and encrypt using the provided
	 * key.
	 *  
	 * @param key   Private key which this class will use to encrypt and decrypt
	 */
	public void setDecryptEncrypt(PaillierPrivateThresholdKey key)
	{
		setDecryption(key);
		setEncryption(key);
		return;
	}

	/**
	 * The public key of the Paillier threshold system, which includes
	 * the values <i>n</i> and the public values <i>v</i> and
	 * {<i>v<sub>i</sub></i>}.  This object must already be in decrypt mode
	 * to return these values.
	 * 
	 * @return     The public key <i>n</i> and public values
	 */
	public PaillierThresholdKey getPublicThresholdKey()
	{
		if(decryptMode)	
			return deckey.getThresholdKey();
		throw new ArgumentException(notReadyForDecryption);
	}

	/** 
	 * The private key for the Paillier system with thresholding
	 * is the RSA modulo n and the secret share <i>s<sub>i</sub></i>
	 * 
	 * @return The private key; null if not in decrypt mode
	 */
	public PaillierPrivateThresholdKey getPrivateKey()
	{
		return decryptMode ? deckey : null;
	}

	/**
	 * Partially decrypts the given ciphertext {@code c} < <i>n</i><sup>2</sup>
	 * using the share of the private key.  Returns only the decrypted value
	 * with no ID attached.
	 * 
	 * @param c    ciphertext as BigInteger
	 * @return     the decrypted share <i>c<sub>i</sub></i>
	 */
	public BigInteger decryptOnly(BigInteger c)
	{
		//Check whether everything is set for doing decryption
		if(decryptMode==false) throw new ArgumentException(notReadyForDecryption);
		
		return new PartialDecryption(deckey, c).GetDecryptedValue();
		
		//if(c.abs().compareTo(key.getNSquare()) >= 0) throw new IllegalArgumentException("c must be less than n^2");

		//return c.modPow(deckey.getSi().multiply(BigInteger.valueOf(2).multiply(this.deckey.getDelta())), deckey.getNSquare());
	}
	
	/**
	 * Partially decrypts the given ciphertext {@code c} < <i>n</i><sup>2</sup>
	 * using the share of the private key.
	 * 
	 * @param c    ciphertext as BigInteger
	 * @return     the decrypted share <i>c<sub>i</sub></i>
	 */
	public PartialDecryption decrypt(BigInteger c)
	{
		//Check whether everything is set for doing decryption
		if(decryptMode==false) throw new ArgumentException(this.notReadyForDecryption);
		
		return new PartialDecryption(deckey, c);
		
		//if(c.abs().compareTo(key.getNSquare()) >= 0) throw new IllegalArgumentException("c must be less than n^2");

		//return c.modPow(deckey.getSi().multiply(BigInteger.valueOf(2).multiply(this.deckey.getDelta())), deckey.getNSquare());
	}

	/**
	 * Partially decrypts the given ciphertext {@code c} < <i>n</i><sup>2</sup>
	 * using the share of the private key.  This then gives a non-interactive
	 * Zero Knowledge Proof that the partial decryption is truly this share's
	 * contribution to the decryption of {@code c}.
	 * 
	 * @param c    ciphertext as BigInteger
	 * @return     the decrypted share <i>c<sub>i</sub></i>
	 */
	public DecryptionZKP decryptProof(BigInteger c) {
		return new DecryptionZKP(deckey, c);
	}

	/**
	 * This function combines the shares of the decryption
	 * to get the final decryption, assumes that the shares are valid.
	 * 
	 * @param shares    a collection of at least <i>w</i> partial decryptions
	 *                  of the same ciphertext
	 * @return          the decrypted value combined using the shares.
	 */
	public BigInteger combineShares(params PartialDecryption[] shares)
	{
		if(decryptMode == false) 
			throw new ArgumentException(notReadyForDecryption);
		if(shares.Length < deckey.getW()) {
			throw new ArgumentException("You must call this method with at least w shares");
		}

		// TODO check to make sure no share is duplicated.
		
		int w = deckey.getW();
		BigInteger delta = deckey.getDelta();
		BigInteger n = deckey.getN();
		BigInteger nSquare = deckey.getNSPlusOne();

		BigInteger cprime = BigInteger.One;
		BigInteger L;
		BigInteger res;

		//System.out.print("c' = ");
		for(var i = 0; i < w; i++) {
			var lambda = delta;
			for(var iprime = 0; iprime < w; iprime++)
			{
				if (iprime == i)
					continue;
				if (shares[i].GetId()-shares[iprime].GetId() != 0)
					lambda = lambda.Multiply(BigInteger.ValueOf(-shares[iprime].GetId())).Divide(BigInteger.ValueOf(shares[i].GetId()-shares[iprime].GetId()));
				else
					throw new ArgumentException("You cannot have repeated shares.");
			}
			cprime = cprime.Multiply(shares[i].GetDecryptedValue().ModPow(BigInteger.ValueOf(2).Multiply(lambda), nSquare)).Mod(nSquare);
			//System.out.print(shares[i]+"^(2*"+lambda+") + ");
		}

		L = cprime.Subtract(BigInteger.One).Divide(n);
		res = L.Multiply(deckey.getCombineSharesConstant()).Mod(n);

		return res;
	}

	/**
	 * This function combines the shares of the decryption
	 * to get the final decryption, all the while checking to make sure
	 * that each ZKP proves a good partial decryption of the correct ciphertext.
	 * 
	 * @param shares    a collection of at least <i>w</i> partial decryptions
	 *                  (with ZKP) of the same ciphertext
	 * @return          the decrypted value combined using the shares.
	 */
	public BigInteger combineShares(params DecryptionZKP[] shares) {
		PartialDecryption[] decryptions = new PartialDecryption[shares.Length];
		BigInteger c = shares[0].getC();
		for (int i = 0; i < shares.Length; i++) {
			if (shares[i].Verify(c)) {
				decryptions[i] = shares[i].getPartialDecryption();
			} else {
				throw new ArgumentException("Someone decrypted the wrong ciphertext");
			}
		}
		return combineShares(decryptions);
	}
}

}