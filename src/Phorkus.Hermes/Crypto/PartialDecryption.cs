using System;
using System.Diagnostics;
using System.IO;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Crypto.Key;

namespace Phorkus.Hermes.Crypto
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
	
	
	
public class PartialDecryption : IEquatable<PartialDecryption> {
	
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
		using (var stream = new MemoryStream(b))
		using (var reader = new BinaryReader(stream))
		{
			var len = reader.ReadInt32();
			var buf = reader.ReadBytes(len);
			decryption = new BigInteger(buf);
			id = reader.ReadInt32();
		}
//		var dec = new byte[b.Length-4];
//		Array.Copy(b, 4, dec, 0, dec.Length);
//		decryption = new BigInteger(dec);
//		id = b[0]<<24 + b[1]<<16 + b[2]<<8 + b[3];
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
		
		using (var memory = new MemoryStream())
		using (var writer = new BinaryWriter(memory))
		{
			var dec = decryption.ToByteArray();
			writer.Write(dec.Length);
			writer.Write(dec);
			writer.Write(id);
			return memory.ToArray();
		}
//		var dec = decryption.ToByteArray();
//		var b = new byte[4+dec.Length];
//        for (var i = 0; i < 4; i++) {
//            var offset = (3 - i) * 8;
//            b[i] = (byte) ((id >> offset) & 0xFF);// b[i] = (byte) ((id >>> offset) & 0xFF);
//        }
//        Array.Copy(dec, 0, b, 4, dec.Length);
//        return b;
	}

	public bool Equals(PartialDecryption other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Equals(decryption, other.decryption) && id == other.id;
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != this.GetType()) return false;
		return Equals((PartialDecryption) obj);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			return ((decryption != null ? decryption.GetHashCode() : 0) * 397) ^ id;
		}
	}
}

}