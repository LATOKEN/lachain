using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Pailler.Key;

namespace Phorkus.Hermes.Pailler
{
/**
 * A partial decryption in the Threshold Paillier encryption scheme with
 * necessary data to proceed with the full decryption.  To produce the full
 * decryption of any ciphertext, at least <i>w</i> decryption servers must
 * provide their partial decryptions.  Furthermore, the source ID of each
 * partial decryption is essential in combining the shares to produce a
 * full and complete decryption of the original ciphertext.  For this reason,
 * a special datatype has been produced to hold only the partial decryption
 * and the source ID.
 * 
 * @author James Garrity
 */
	
	
	
public class PartialDecryption{
	
	/*
	 * 
	 * Fields
	 * 
	 */

	/**
	 * This Serial ID
	 */
	private static long serialVersionUID = -6668831686028175205L;
	
	/** The partial decryption */
	private BigInteger decryption;
	
	/** The ID number of the decryption server who decrypted this. */
	private int id;
	
	/*
	 * 
	 * Constructors
	 * 
	 */
	
	/**
	 * Links the partial decryption {@code decryption} as coming from
	 * decryption server {@code id}.
	 * 
	 * @param decryption     a partial decryption
	 * @param id             the id of the secret key who composed this
	 *                       partial decryption
	 */
	public PartialDecryption(BigInteger decryption, int id) {
		this.decryption = decryption;
		this.id = id;
	}
	
	/**
	 * Translates a byte array, of which the first four bytes contain the id
	 * and the last number of bytes contain the two's complement binary
	 * representation of a BigInteger.
	 * 
	 * @param b
	 */
	public PartialDecryption(byte[] b) {
		var dec = new byte[b.Length-4];
		Array.Copy(b, 4, dec, 0, dec.Length);
		decryption = new BigInteger(dec);
		id = b[0]<<24 + b[1]<<16 + b[2]<<8 + b[3];
	}
	
	/**
	 * Computes the partial decryption of {@code ciphertext} using the
	 * private key {@code key}.  This is essentially the value
	 * {@code ciphertext}<sup>2&Delta;<i>s<sub>i</sub></i></sup>.
	 * 
	 * @param key            private key of decryption server <i>i</i>
	 * @param ciphertext     original ciphertext
	 */
	public PartialDecryption(PaillierPrivateThresholdKey key, BigInteger ciphertext) {
		//Check whether everything is set for doing decryption
		if(!key.inModNSPlusOne(ciphertext)) throw new ArgumentException("c must be less than n^2");

		decryption = ciphertext.ModPow(key.getSi().Multiply(BigInteger.ValueOf(2).Multiply(key.getDelta())), key.getNSPlusOne());
		id = key.getID();
	}
	
	/*
	 * 
	 * Methods
	 * 
	 */
	
	/**
	 * Returns the partial decryption string
	 * 
	 * @return          the value <i>c<sub>i</sub></i>
	 */
	public BigInteger GetDecryptedValue() {
		return decryption;
	}
	
	/**
	 * Returns the ID of the secret key which produced this partial decryption
	 * 
	 * @return          the secret key ID used
	 */
	public int GetId() {
		return id;
	}
	
	/**
	 * Returns a byte array where the first four bytes signify the ID and the
	 * remaining signify the partial decryption.
	 * 
	 * @return			byte array of the ID concatenated to byte array of
	 * 					the partial decryption.
	 */
	public byte[] toByteArray() {
		// The encoding would be
		// [ id ]
		// [ decryption ]
		
		var dec = decryption.ToByteArray();
		var b = new byte[4+dec.Length];
        for (var i = 0; i < 4; i++) {
            var offset = (3 - i) * 8;
            b[i] = (byte) ((id >> offset) & 0xFF);// b[i] = (byte) ((id >>> offset) & 0xFF);
        }
        Array.Copy(dec, 0, b, 4, dec.Length);
        return b;
	}
}

}