/*
* Copyright 2008 ZXing authors
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
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

// Namespace changed from "ZXing.Common.ReedSolomon" to "STH1123.ReedSolomon" by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
namespace STH1123.ReedSolomon
{
    /// <summary>
    /// Implements Reed-Solomon encoding, as the name implies.
    /// </summary>
    /// <author>Sean Owen</author>
    /// <author>William Rucklidge</author>
    public sealed class ReedSolomonEncoder
    {
        private readonly GenericGF field;
        private readonly IList<GenericGFPoly> cachedGenerators;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReedSolomonEncoder"/> class.
        /// </summary>
        /// <param name="field">A <see cref="GenericGF"/> that represents the Galois field to use</param>
        public ReedSolomonEncoder(GenericGF field)
        {
            this.field = field;
            this.cachedGenerators = new List<GenericGFPoly>();
            cachedGenerators.Add(new GenericGFPoly(field, new int[] { 1 }, true));
        }

        private GenericGFPoly buildGenerator(int degree)
        {
            if (degree >= cachedGenerators.Count)
            {
                var lastGenerator = cachedGenerators[cachedGenerators.Count - 1];
                for (int d = cachedGenerators.Count; d <= degree; d++)
                {
                    var nextGenerator = lastGenerator.multiply(new GenericGFPoly(field, new int[] { 1, field.exp(d - 1 + field.GeneratorBase) }, true));
                    cachedGenerators.Add(nextGenerator);
                    lastGenerator = nextGenerator;
                }
            }
            return cachedGenerators[degree];
        }

        /// <summary>
        /// Encodes given set of data codewords with Reed-Solomon.
        /// </summary>
        /// <param name="toEncode">data codewords and padding, the amount of padding should match
        /// the number of error-correction codewords to generate. After encoding, the padding is
        /// replaced with the error-correction codewords</param>
        /// <param name="ecBytes">number of error-correction codewords to generate</param>
        public void Encode(int[] toEncode, int ecBytes)
        {
            // Method modified by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            // added check for messages that are too long for the used Galois field

            if (toEncode.Length >= field.Size)
            {
                throw new ArgumentException("Message is too long for this field", "toEncode");
            }

            if (ecBytes <= 0)
            {
                throw new ArgumentException("No error correction bytes provided", "ecBytes");
            }
            var dataBytes = toEncode.Length - ecBytes;
            if (dataBytes <= 0)
            {
                throw new ArgumentException("No data bytes provided", "ecBytes");
            }

            var generator = buildGenerator(ecBytes);
            var infoCoefficients = new int[dataBytes];
            Array.Copy(toEncode, 0, infoCoefficients, 0, dataBytes);

            var info = new GenericGFPoly(field, infoCoefficients, true);
            info = info.multiplyByMonomial(ecBytes, 1);

            var remainder = info.divide(generator)[1];
            var coefficients = remainder.Coefficients;
            var numZeroCoefficients = ecBytes - coefficients.Length;
            for (var i = 0; i < numZeroCoefficients; i++)
            {
                toEncode[dataBytes + i] = 0;
            }

            Array.Copy(coefficients, 0, toEncode, dataBytes + numZeroCoefficients, coefficients.Length);
        }

    }
}