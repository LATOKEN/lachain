using System;
using Org.BouncyCastle.Math;
using Phorkus.Party.Crypto.Key;

namespace Phorkus.Party.Crypto
{
	/**
	 * A simple implementation of the generalized Paillier encryption scheme
	 * <i>CS</i><sub>1</sub>.  This is based on the gernarlization given in
	 * <i>Generalization of Paillier's Public-Key System with Applications to
	 * Electronic Voting</i> by Damg&aring;rd et al. with the parameter <b><i>s</i> fixed
	 * at 1.</b>
	 * <p>
	 * With most of the methods already defined in {@link AbstractPaillier}, this
	 * class provides the essential methods of encryption and decryption in the
	 * simplified Paillier encryption scheme, as well as a few test/diagnostic
	 * methods.
	 * 
	 * <h3>Paillier Encryption Scheme</h3>
	 * The Paillier encryption scheme is a probabilistic asymmetric encryption
	 * scheme with homomorphic properties for both addition and multiplication.  It
	 * takes plaintext <i>i</i> less than <i>n</i> to compute the encryption
	 * <i>E(i,r)</i> for a random <i>r</i> (with <i>n</i> predetermined by the
	 * public key).  Damg&aring;rd et al. constructed a
	 * generalized version of the scheme to allow more freedom in changing the block
	 * size of the encryption independently of the size of the public key.
	 * Whereas Paillier's original scheme created plaintexts less than <i>n</i>,
	 * this generalized version allows plaintexts to be less than
	 * <i>n<sup>s</sup></i>.  This implementation is of Paillier's original scheme
	 * as expressed in the generalized version of Damg&aring;rd et al. (i.e.
	 * <i>s</i> fixed to 1).
	 * <p>
	 * <b>The Math:</b> The simplified Paillier encryption scheme takes a
	 * {@link paillierp.key.PaillierKey PaillierKey} <i>n</i> to encrypt a plaintext
	 *  <i>i</i> in <i>Z<sub>n<sup>s</sup></sub></i> by choosing a random
	 * <i>r</i>&isin;<i>Z<sub>n</sub></i><sup>*</sup> by simply computing
	 * (<i>n</i>+1)<i><sup>i</sup>r<sup>n</sup></i> mod <i>n</i><sup><i>s</i>+1</sup>.
	 * If given a {@link paillierp.key.PaillierPrivateKey PaillierPrivateKey}
	 * <i>d</i>, raising a ciphertext <i>c</i> to the power <i>d</i> gives a value
	 * (1+<i>n</i>)<sup><i>id</i></sup>, and by using a method in the paper, can
	 * give the original message <i>i</i> mod <i>n<sup>s</sup></i>.
	 * <p>
	 * Note that the random number
	 * generator is included in the key object.  (The default is
	 * {@link java.security.SecureRandom}.)
	 * <p>
	 * Future expansions will include support for encrypting arbitrary length
	 * strings/byte arrays to avoid padding issues, and support for padding.
	 *  
	 * @author Murat Kantarcioglu
	 * @author James Garrity
	 * @author Sean Hall
	 * @see    paillierp.AbstractPaillier
	 */
	public class Paillier : AbstractPaillier{
	
		/*
		 * 
		 * Fields
		 * 
		 */
	
		/** Private Key allowing decryption; should be same as public key. */
		protected PaillierPrivateKey deckey = null;
	
		/*
		 * 
		 * Constructors
		 * 
		 */
	
		/**
		 * Default constructor. This constructor can be used if there is 
		 * no need to generate public/private key pair.
		 */
		public Paillier(){}
		
		/**
		 * Constructs a new encryption object which uses the specified
		 * key for encryption.
		 * 
		 * @param key  Public key used for encryption
		 */
		public Paillier(PaillierKey key) {
			this.key = key;
			
			this.encryptMode = true;
		}
		
		/**
		 * Constructs a new encryption/decryption object which uses the specified
		 * key for both encryption and decryption.
		 * 
		 * @param key  Private key used for decryption and encryption
		 */
		public Paillier(PaillierPrivateKey key)
		{
			this.key = key.getPublicKey();  //this(key.getPublicKey());
			setDecryption(key);
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
		public void setEncryption(PaillierKey key)
		{
			/*if (this.decryptMode==false || this.deckey.getN() == key.getN()){
				this.key = key;
			}
			else {
				throw new IllegalArgumentException("Given public key does not correspond to stored private key");
			}*/
			
			this.key = key;
	
			// Enable the encryption mode now
			this.encryptMode=true;
	
			return;
		}
	
		/**
		 * Sets the mode for this object to decrypt and will use the provided key
		 * to decrypt only.  (Encryption will continue to be done using the key 
		 * provided in {@link #setEncryption(PaillierKey)}.)
		 * 
		 * @param key Private key which this class will use to decrypt
		 */
		public void setDecryption(PaillierPrivateKey key)
		{
			this.deckey = key;
	
			// enable the decryption mode now
			this.decryptMode=true;
			return;
		}
	
		/**
		 * Sets the mode for this object to decrypt and encrypt using the provided
		 * key.
		 *  
		 * @param key   Private key which this class will use to encrypt and decrypt
		 */
		public void setDecryptEncrypt(PaillierPrivateKey key)
		{
			setDecryption(key);
			setEncryption(key);
			return;
		}
	
		/** 
		 * Returns the current private key in use by this encryption object.
		 * 
		 * @return      The private key used; returns {@code null} if this is not
		 *              in decryption mode.
		 */
		public PaillierPrivateKey getPrivateKey()
		{  
			if (decryptMode) {
				return deckey;
			} else {
				return null;
			}
		}
	
		/**
		 * Decrypts the given ciphertext.
		 * 
		 * @param c     Ciphertext as BigInteger c
		 * @return      Decrypted value D(c) as BigInteger
		 */
		public BigInteger decrypt(BigInteger c)
		{
			//Check whether everything is set for doing decryption
			if(decryptMode==false) throw new ArgumentException(this.notReadyForDecryption);
			if(!(key.inModNSPlusOne(c))) throw new ArgumentException("c must be less than n^2");
	
			BigInteger c1=null;
	
			//first we calculate c^d mod n^2		
			c1= c.ModPow(deckey.getD(),deckey.getNSPlusOne());
	
			//after we calculate c1=c^d mod n^2 = (1+n)^(m*d mod n)
			// we now find (c1-1)/n=m*d mod n
			//therefore m= d^-1*(c1-1)/n mod n
			// TODO: Is this true?
			return (deckey.getDInvs().Multiply((c1.Subtract(BigInteger.One)).Divide(deckey.getN()))).Mod(deckey.getN());
		}
	
		/*
		 * 
		 * Feature Tests
		 * 
		 */
		
		/**
		 * This main method basically tests the different features of the
		 * Paillier encryption
		 */
		public static void test()
		{
			Random rd=new Random();
			long num=0;
			long num1=0;
			BigInteger m=null;
			BigInteger c=null;
			int numberOfTests=10;
			int j=0;
			BigInteger decryption=null;
			Paillier esystem= new Paillier();
			PaillierPrivateKey key=KeyGen.PaillierKey(512,122333356);
			esystem.setDecryptEncrypt(key);
			//let's test our algorithm by encrypting and decrypting a few instances
			for(int i=0; i<numberOfTests; i++)
			{
				num=System.Math.Abs((long)rd.NextDouble());
				m=BigInteger.ValueOf(num);
				Console.WriteLine("number chosen  : " +m);
				c=esystem.encrypt(m);
				Console.WriteLine("encrypted value: "+c);
				decryption=esystem.decrypt(c);
				Console.WriteLine("decrypted value: "+decryption);
				if(m.CompareTo(decryption)==0)
				{
					Console.WriteLine("OK");
					j++;
				}
				else
					Console.WriteLine("PROBLEM"); 
			}
			Console.WriteLine("out of "+ numberOfTests
					+"random encryption,# many of "+ j
					+" has passed");
			// Let us check the commutative properties of the paillier encryption
			Console.WriteLine("Checking the additive properteries of the Paillier encryption" );
			//   Obviously 1+0=1
			Console.WriteLine("1+0="+ esystem.decrypt(
					esystem.add(
							esystem.encryptone(),
							esystem.encryptzero()
					)
			)
			);
			// 1+1=2
			Console.WriteLine("1+1="+esystem.decrypt(
					esystem.add(
							esystem.encrypt(BigInteger.One),
							esystem.encrypt(BigInteger.One)
					)
			));
	
			// 1+1+1=3
			Console.WriteLine("1+1+1="+esystem.decrypt(
					esystem.add( 
							esystem.add(
									esystem.encrypt(BigInteger.One),
									esystem.encrypt(BigInteger.One)
							),
							esystem.encrypt(BigInteger.One)
					) 
			));
	
			// 0+0=0
			Console.WriteLine("0+0="+ esystem.decrypt(
					esystem.add(
							esystem.encrypt(BigInteger.Zero),
							esystem.encrypt(BigInteger.Zero)
					)
			));
			// 1+-1=0  
			Console.WriteLine("1+-1="+ esystem.decrypt(
					esystem.add(
							esystem.encrypt(BigInteger.ValueOf(-1).Mod(key.getN())),
							esystem.encrypt(BigInteger.One)
					)
			)); 
	
			do {
				num=(long)rd.NextDouble();
			} while(key.inModN(BigInteger.ValueOf(num)) == false);
			do {
				num1=(long)rd.NextDouble();
			} while(key.inModN(BigInteger.ValueOf(num1)) == false);
			BigInteger numplusnum1 = BigInteger.ValueOf(num).Add(BigInteger.ValueOf(num1));
			BigInteger summodnsquare = numplusnum1.Mod(key.getN());
			//D(E(num)+E(num1))=num+num1
			Console.WriteLine(numplusnum1.ToString());
			Console.WriteLine(summodnsquare.ToString() + "=\n"
					+esystem.decrypt(
							esystem.add(
									esystem.encrypt(BigInteger.ValueOf(num)),
									esystem.encrypt(BigInteger.ValueOf(num1))
							)
					));    
			// Let us check the multiplicative properties
			Console.WriteLine("Checking the multiplicative properties");
			// D(multiply(E(2),3))=6
			Console.WriteLine("6="+ esystem.decrypt(esystem.multiply(esystem.add(
					esystem.encrypt(BigInteger.One),
					esystem.encrypt(BigInteger.One)
			),3
			))
			);
	
		}
	
	
		public static void testICDE()
		{
			// Number of total operations
			int numberOfTests=5;
			//Length of the p, note that n=p.q
			int lengthp=512;
	
			Paillier esystem= new Paillier();
			Random rd=new Random();
			PaillierPrivateKey key=KeyGen.PaillierKey(lengthp,122333356);
			esystem.setDecryptEncrypt(key);
			//let's test our algorithm by encrypting and decrypting few instances
	
	
			long start = DateTime.Now.Millisecond; 
			for(int i=0; i<numberOfTests; i++)
			{
				BigInteger m1=BigInteger.ValueOf(System.Math.Abs((long)rd.NextDouble()));
				BigInteger m2=BigInteger.ValueOf(System.Math.Abs((long)rd.NextDouble()));
				BigInteger c1=esystem.encrypt(m1);
				BigInteger c2=esystem.encrypt(m2);
				BigInteger c3=esystem.multiply(c1,m2);
				c1=esystem.add(c1,c2);
				c1=esystem.add(c1,c3);
	
				esystem.decrypt(c1);
			}
			
			long stop = DateTime.Now.Millisecond;
			Console.WriteLine("Running time per comparison in milliseconds: "
					+ (stop-start)/numberOfTests);
		}
	}
}