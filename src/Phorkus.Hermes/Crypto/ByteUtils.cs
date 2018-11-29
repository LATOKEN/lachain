using System;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Crypto
{
/**
 * A set of static utilities for manipulating Bytes.  The methods used in this
 * class are used primarily for encoding integers and BigIntegers into byte
 * arrays and vice versa.
 * 
 * @author James Garrity
 *
 */
public class ByteUtils {
	
	/*
	 * 
	 * Fields
	 * 
	 */
	
	/** A string array for representing binary numbers. */
	public static String[] base2 = {"0", "1"};

	/*
	 * 
	 * Methods
	 * 
	 */
	
	/**
	 * Converts an integer into its 4-byte equivalent in Big-endan form.
	 * 
	 * @param i				32 bit integer
	 * @return				a byte array of size 4 with most sig. bits first
	 */
	public static byte[] intToByte(int i) {
		return new byte[] {(byte)(i>>24), (byte)(i>>16), (byte)(i>>8), (byte)i};
	}
	
	/**
	 * Determines the integer given in the first four bytes of a given array
	 * starting with the offset.
	 * 
	 * @param b				Byte array of size greater than 4
	 * @param offset		Index of b
	 * @return				an integer equivalent to the four subsequent bytes
	 * 						starting at offset
	 */
	public static int getInt(byte[] b, int offset) {
		return ((b[offset] & 0xff) << 24) | ((b[offset+1] & 0xff)<< 16) |
					((b[offset+2] & 0xff) << 8) | (b[offset+3] & 0xff);
	}
	
	/**
	 * Creates a BigInteger equivalent to the {@code size} bytes given in
	 * {@code b} starting at offset.
	 * 
	 * @param b				Byte array
	 * @param offset		Index of b; where to start
	 * @param size			how long to go
	 * @return				a BigInteger equivalent to the subsequent bytes
	 * 						starting at offset
	 */
	public static BigInteger getBigInt(byte[] b, int offset, int size) {
		byte[] temp = new byte[size];
		Array.Copy(b, offset, temp, 0, size);
		return new BigInteger(temp);
	}
	
	/**
	 * Returns the first <i>x</i> bytes of {@code b}, where <i>x</i> is the 
	 * base 10 equivalent to the last four bytes of {@code b}.
	 * 
	 * @param b				A byte array, the last four of which represent
	 * 						an integer <i>x</i>
	 * @return				The first <i>x</i> bytes of {@code b}.
	 */
	public static byte[] getLowerLayer(byte[] b) {
		// TODO check size of b >= 4
		int sizeofLowerLayer = getInt(b, b.Length - 4);
		// TODO check size of b >= sizeofLowerLayer+4
		byte[] r = new byte[sizeofLowerLayer];
		Array.Copy(b, 0, r, 0, sizeofLowerLayer);
		return r;
	}
	
	/**
	 * Returns a byte array where the first bytes are {@code b}, followed
	 * by each of {@code bigIntegers} in byte array form, each with its
	 * length encoded in 4 bytes.
	 * 
	 * @param b				A byte array
	 * @param bigIntegers	BigIntegers <i>i</i><sub>1</sub>, ...
	 * 						<i>i</i><sub>m</sub>
	 * @return				A byte array of {@code b || len[}
	 * 						<i>i</i><sub>1</sub> {@code ] || }
	 * 						<i>i</i><sub>1</sub> {@code || ... || len[}
	 * 						<i>i</i><sub>m</sub> {@code ] || }
	 * 						<i>i</i><sub>m</sub>.  A byte array of length 0
	 * 						is returned if (1) the concatenation would be too 
	 * 						large for an integer index or (2) {@code b} is
	 * 						of length 0.
	 * 
	 * @see BigInteger#toByteArray()
	 */
	public static byte[] appendBigInt(byte[] b, params BigInteger[] bigIntegers) {
		if(bigIntegers.Length == 0) throw new ArgumentException("You must supply at least one int");
		
		if (b.Length == 0) return b;
		
		byte[] r;
		
		long len = (long) b.Length;
		
		int[] lengths = new int[bigIntegers.Length];
		byte[][] dataarray = new byte[bigIntegers.Length][];
		for (int i = 0; i < bigIntegers.Length; i++) {
			dataarray[i] = bigIntegers[i].ToByteArray();
			lengths[i] = dataarray[i].Length;
			len += lengths[i]+4;
		}
		
		if (len > int.MaxValue) {
			r = new byte[0];
		} else {
			r = new byte[(int)len];
			
			Array.Copy(b, 0, r, 0, b.Length);
			int offset = b.Length;
			for (int i = 0; i < bigIntegers.Length; i++) {
				Array.Copy(intToByte(lengths[i]), 0, r, offset, 4);
				Array.Copy(dataarray[i], 0, r, offset+4, lengths[i]);
				offset += lengths[i]+4;
			}
		}
		
		return r;
	}
	
	/**
	 * Returns a byte array where the first bytes are {@code b}, followed
	 * by each of {@code ints} in 4-byte array form.
	 * 
	 * @param b				A byte array
	 * @param ints			Integers <i>i</i><sub>1</sub>, ...
	 * 						<i>i</i><sub>m</sub>
	 * @return				A byte array of {@code b || }
	 * 						<i>i</i><sub>1</sub> {@code || ... ||}
	 * 						<i>i</i><sub>m</sub>.  A byte array of length 0
	 * 						is returned if (1) the concatenation would be too
	 * 						large for an integer index or (2) {@code b} is
	 * 						of length 0.
	 */
	public static byte[] appendInt(byte[] b, params int[] ints) {
		if(ints.Length == 0) throw new ArgumentException("You must supply at least one int");
		
		if (b.Length == 0) return b;
		
		byte[] r;
		
		if (b.Length + ints.Length * 4 >= int.MaxValue) {
			r = new byte[0];
		} else {
			r = new byte[b.Length + ints.Length*4];
			
			Array.Copy(b, 0, r, 0, b.Length);
			int offset = b.Length;
			for (int i = 0; i < ints.Length; i++) {
				Array.Copy(intToByte(ints[i]), 0, r, offset, 4);
				offset += 4;
			}
		}
		
		return r;
	}
	
	/**
	 * Displays the given byte in a string of 0's and 1's.
	 * 
	 * @param b		A simple byte
	 * @return		A string of the binary representation of {@code b}
	 */
	public static String printByte(byte b) {
		String output = "";
		
		for (int i = 7; i >=0; i--) {
			output += base2[(int)((b >> i) & 0x01)];
		}
		
		return output;
	}
}

}