/*
 * Copyright 2007 ZXing authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

// Namespace changed from "ZXing.Common.ReedSolomon" to "STH1123.ReedSolomon" by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
namespace Lachain.Consensus.ReliableBroadcast.ReedSolomon.ReedSolomon
{
    /// <summary>
    ///   <p>This class contains utility methods for performing mathematical operations over
    /// the Galois Fields. Operations use a given primitive polynomial in calculations.</p>
    ///   <p>Throughout this package, elements of the GF are represented as an <see cref="Int32"/>
    /// for convenience and speed (but at the cost of memory).
    ///   </p>
    /// </summary>
    /// <author>Sean Owen</author>
    public sealed class GenericGF
    {
        /// <summary>
        /// x^12 + x^6 + x^5 + x^3 + 1
        /// </summary>
        public static GenericGF AZTEC_DATA_12 = new GenericGF(0x1069, 4096, 1, 2); // x^12 + x^6 + x^5 + x^3 + 1

        /// <summary>
        /// x^10 + x^3 + 1
        /// </summary>
        public static GenericGF AZTEC_DATA_10 = new GenericGF(0x409, 1024, 1, 2); // x^10 + x^3 + 1

        /// <summary>
        /// x^6 + x + 1
        /// </summary>
        public static GenericGF AZTEC_DATA_6 = new GenericGF(0x43, 64, 1, 2); // x^6 + x + 1

        /// <summary>
        /// x^4 + x + 1
        /// </summary>
        public static GenericGF AZTEC_PARAM = new GenericGF(0x13, 16, 1, 2); // x^4 + x + 1

        /// <summary>
        /// x^8 + x^4 + x^3 + x^2 + 1
        /// </summary>
        public static GenericGF QR_CODE_FIELD_256 = new GenericGF(0x011D, 256, 0, 2); // x^8 + x^4 + x^3 + x^2 + 1

        /// <summary>
        /// x^8 + x^5 + x^3 + x^2 + 1
        /// </summary>
        public static GenericGF DATA_MATRIX_FIELD_256 = new GenericGF(0x012D, 256, 1, 2); // x^8 + x^5 + x^3 + x^2 + 1

        /// <summary>
        /// x^8 + x^5 + x^3 + x^2 + 1
        /// </summary>
        public static GenericGF AZTEC_DATA_8 = DATA_MATRIX_FIELD_256;

        /// <summary>
        /// x^6 + x + 1
        /// </summary>
        public static GenericGF MAXICODE_FIELD_64 = AZTEC_DATA_6;

        private int[] expTable;
        private int[] logTable;
        private readonly int size;
        private readonly int primitive;
        private readonly int generatorBase;
        private readonly int alpha;

        /// <summary>
        /// Create a representation of GF(size) using the given primitive polynomial.
        /// </summary>
        /// <param name="primitive">irreducible polynomial whose coefficients are represented by
        /// the bits of an <see cref="Int32"/>, where the least-significant bit represents the constant
        /// coefficient</param>
        /// <param name="size">the size of the field</param>
        /// <param name="genBase">the factor b in the generator polynomial can be 0- or 1-based
        /// (g(x) = (x+a^b)(x+a^(b+1))...(x+a^(b+2t-1))).
        /// In most cases it should be 1, but for QR code it is 0.</param>
        public GenericGF(int primitive, int size, int genBase)
            : this(primitive, size, genBase, 2)
        {
            // Constructor added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            // calls overloaded constructor only
        }

        /// <summary>
        /// Create a representation of GF(size) using the given primitive polynomial.
        /// </summary>
        /// <param name="primitive">irreducible polynomial whose coefficients are represented by
        /// the bits of an <see cref="Int32"/>, where the least-significant bit represents the constant
        /// coefficient</param>
        /// <param name="size">the size of the field</param>
        /// <param name="genBase">the factor b in the generator polynomial can be 0- or 1-based
        /// (g(x) = (x+a^b)(x+a^(b+1))...(x+a^(b+2t-1))).
        /// In most cases it should be 1, but for QR code it is 0.</param>
        /// <param name="alpha">the generator alpha</param>
        public GenericGF(int primitive, int size, int genBase, int alpha)
        {
            // Constructor modified by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            // to add support for alpha powers other than 2
            this.primitive = primitive;
            this.size = size;
            this.generatorBase = genBase;
            this.alpha = alpha;

            expTable = new int[size];
            logTable = new int[size];
            int x = 1;
            if (alpha == 2)
            {
                for (int i = 0; i < size; i++)
                {
                    expTable[i] = x;
                    x <<= 1; // x = x * 2; the generator alpha is 2
                    if (x >= size)
                    {
                        x ^= primitive;
                        x &= size - 1;
                    }
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    expTable[i] = x;
                    x = multiplyNoLUT(x, alpha, primitive, size);
                }
            }
            for (int i = 0; i < size - 1; i++)
            {
                logTable[expTable[i]] = i;
            }
            // logTable[0] == 0 but this should never be used
        }

        // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        static internal int multiplyNoLUT(int x, int y, int primitive, int size)
        {
            int r = 0;
            while (y > 0)
            {
                if (Convert.ToBoolean(y & 1))
                {
                    r ^= x;
                }
                y >>= 1;
                x <<= 1;
                if (x >= size)
                {
                    x ^= primitive;
                    x &= size - 1;
                }
            }
            return r;
        }

        /// <summary>
        /// Implements both addition and subtraction -- they are the same in GF(size).
        /// </summary>
        /// <returns>sum/difference of a and b</returns>
        static internal int addOrSubtract(int a, int b)
        {
            return a ^ b;
        }

        /// <summary>
        /// Exps the specified a.
        /// </summary>
        /// <returns>alpha to the power of a in GF(size)</returns>
        internal int exp(int a)
        {
            return expTable[a];
        }

        /// <summary>
        /// Logs the specified a.
        /// </summary>
        /// <param name="a">A.</param>
        /// <returns>base alpha log of a in GF(size)</returns>
        internal int log(int a)
        {
            if (a == 0)
            {
                throw new ArithmeticException("log(0) is undefined");
            }
            return logTable[a];
        }

        /// <summary>
        /// Inverses the specified a.
        /// </summary>
        /// <returns>multiplicative inverse of a</returns>
        internal int inverse(int a)
        {
            if (a == 0)
            {
                throw new ArithmeticException("inverse(0) is undefined");
            }
            // todo: reduce input a and b to range (1, size - 1) otherwise out of range
            // a = a % (size - 1);
            return expTable[size - logTable[a] - 1];
        }

        /// <summary>
        /// Multiplies the specified a with b.
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns>product of a and b in GF(size)</returns>
        internal int multiply(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            // todo: reduce input a and b to range (1, size - 1) otherwise out of range
            // a = a % (size - 1);
            // b = b % (size - 1);
            if(a > size || b > size)
                Console.Error.WriteLine($"a {0} b {1}", a, b);
            return expTable[(logTable[a] + logTable[b]) % (size - 1)];
        }

        /// <summary>
        /// Gets the primitive polynomial as an <see cref="Int32"/>.
        /// </summary>
        public int Primitive
        {
            // Property added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            get { return primitive; }
        }

        /// <summary>
        /// Gets the size.
        /// </summary>
        public int Size
        {
            get { return size; }
        }

        /// <summary>
        /// Gets the generator base.
        /// </summary>
        public int GeneratorBase
        {
            get { return generatorBase; }
        }

        /// <summary>
        /// Gets the generator alpha.
        /// </summary>
        public int Alpha
        {
            // Property added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            get { return alpha; }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        override public String ToString()
        {
            return "GF(0x" + primitive.ToString("X") + ',' + size + ',' + alpha + ')';
        }
    }
}