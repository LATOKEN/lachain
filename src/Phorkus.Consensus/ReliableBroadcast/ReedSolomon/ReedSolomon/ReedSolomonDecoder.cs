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
using System.Collections.Generic;

// Namespace changed from "ZXing.Common.ReedSolomon" to "STH1123.ReedSolomon" by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
namespace STH1123.ReedSolomon
{
    /// <summary> <p>Implements Reed-Solomon decoding, as the name implies.</p>
    /// 
    /// <p>The algorithm will not be explained here, but the following references were helpful
    /// in creating this implementation:</p>
    /// 
    /// <ul>
    /// <li>Bruce Maggs.
    /// <a href="http://www.cs.cmu.edu/afs/cs.cmu.edu/project/pscico-guyb/realworld/www/rs_decode.ps">
    /// "Decoding Reed-Solomon Codes"</a> (see discussion of Forney's Formula)</li>
    /// <li>J.I. Hall. <a href="www.mth.msu.edu/~jhall/classes/codenotes/GRS.pdf">
    /// "Chapter 5. Generalized Reed-Solomon Codes"</a>
    /// (see discussion of Euclidean algorithm)</li>
    /// </ul>
    /// 
    /// <p>Much credit is due to William Rucklidge since portions of this code are an indirect
    /// port of his C++ Reed-Solomon implementation.</p>
    /// 
    /// </summary>
    /// <author>Sean Owen</author>
    /// <author>William Rucklidge</author>
    /// <author>sanfordsquires</author>
    public sealed class ReedSolomonDecoder
    {
        private readonly GenericGF field;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReedSolomonDecoder"/> class.
        /// </summary>
        /// <param name="field">A <see cref="GenericGF"/> that represents the Galois field to use</param>
        public ReedSolomonDecoder(GenericGF field)
        {
            this.field = field;
        }

        /// <summary>
        ///   <p>Decodes given set of received codewords, which include both data and error-correction
        /// codewords. Really, this means it uses Reed-Solomon to detect and correct errors, in-place,
        /// in the input.</p>
        /// </summary>
        /// <param name="received">data and error-correction codewords</param>
        /// <param name="twoS">number of error-correction codewords available</param>
        /// <returns>false: decoding fails</returns>
        public bool Decode(int[] received, int twoS)
        {
            // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            return Decode(received, twoS, null);
        }

        /// <summary>
        ///   <p>Decodes given set of received codewords, which include both data and error-correction
        /// codewords. Really, this means it uses Reed-Solomon to detect and correct errors, in-place,
        /// in the input.</p>
        /// </summary>
        /// <param name="received">data and error-correction codewords</param>
        /// <param name="twoS">number of error-correction codewords available</param>
        /// <param name="erasurePos">array of zero-based erasure indices</param>
        /// <returns>false: decoding fails</returns>
        public bool Decode(int[] received, int twoS, int[]? erasurePos)
        {
            // Method modified by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
            // to add support for erasure and errata correction
            // most code ported to C# from the python code at http://en.wikiversity.org/wiki/Reed–Solomon_codes_for_coders

            if (received.Length >= field.Size)
            {
                throw new ArgumentException("Message is too long for this field", "received");
            }

            if (twoS <= 0)
            {
                throw new ArgumentException("No error correction bytes provided", "twoS");
            }
            var dataBytes = received.Length - twoS;
            if (dataBytes <= 0)
            {
                throw new ArgumentException("No data bytes provided", "twoS");
            }

            var syndromeCoefficients = new int[twoS];
            var noError = true;

            if (erasurePos == null)
            {
                erasurePos = new int[] { };
            }
            else
            {
                for (var i = 0; i < erasurePos.Length; i++)
                {
                    received[erasurePos[i]] = 0;
                }
            }

            if (erasurePos.Length > twoS)
            {
                return false;
            }

            var poly = new GenericGFPoly(field, received, false);

            for (var i = 0; i < twoS; i++)
            {
                var eval = poly.evaluateAt(field.exp(i + field.GeneratorBase));
                syndromeCoefficients[syndromeCoefficients.Length - 1 - i] = eval;
                if (eval != 0)
                {
                    noError = false;
                }
            }
            if (noError)
            {
                return true;
            }

            var syndrome = new GenericGFPoly(field, syndromeCoefficients, false);

            var forneySyndrome = calculateForneySyndromes(syndrome, erasurePos, received.Length);

            var sigma = runBerlekampMasseyAlgorithm(forneySyndrome, erasurePos.Length);

            if (sigma == null)
            {
                return false;
            }

            var errorLocations = findErrorLocations(sigma);
            if (errorLocations == null)
            {
                return false;
            }

            // Prepare errors
            int[] errorPositions = new int[errorLocations.Length];

            for (int i = 0; i < errorLocations.Length; i++)
            {
                errorPositions[i] = field.log(errorLocations[i]);
            }

            // Prepare erasures
            int[] erasurePositions = new int[erasurePos.Length];

            for (int i = 0; i < erasurePos.Length; i++)
            {
                erasurePositions[i] = received.Length - 1 - erasurePos[i];
            }

            // Combine errors and erasures
            int[] errataPositions = new int[errorPositions.Length + erasurePositions.Length];

            Array.Copy(errorPositions, 0, errataPositions, 0, errorPositions.Length);
            Array.Copy(erasurePositions, 0, errataPositions, errorPositions.Length, erasurePositions.Length);

            var errataLocator = findErrataLocator(errataPositions);
            var omega = findErrorEvaluator(syndrome, errataLocator);

            if (omega == null)
            {
                return false;
            }

            int[] errata = new int[errataPositions.Length];

            for (int i = 0; i < errataPositions.Length; i++)
            {
                errata[i] = field.exp(errataPositions[i]);
            }

            var errorMagnitudes = findErrorMagnitudes(omega, errata);

            if (errorMagnitudes == null)
            {
                return false;
            }

            for (var i = 0; i < errata.Length; i++)
            {
                var position = received.Length - 1 - field.log(errata[i]);
                if (position < 0)
                {
                    // throw new ReedSolomonException("Bad error location");
                    return false;
                }
                received[position] = GenericGF.addOrSubtract(received[position], errorMagnitudes[i]);
            }

            var checkPoly = new GenericGFPoly(field, received, false);

            var error = false;

            for (var i = 0; i < twoS; i++)
            {
                var eval = checkPoly.evaluateAt(field.exp(i + field.GeneratorBase));
                if (eval != 0)
                {
                    error = true;
                }
            }
            if (error)
            {
                return false;
            }

            return true;
        }

        // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        internal GenericGFPoly calculateForneySyndromes(GenericGFPoly syndromes, int[] positions, int messageLength)
        {
            int[] positionsReversed = new int[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                positionsReversed[i] = messageLength - 1 - positions[i];
            }

            int forneySyndromesLength = syndromes.Coefficients.Length;

            int[] syndromeCoefficients = new int[syndromes.Coefficients.Length];
            Array.Copy(syndromes.Coefficients, 0, syndromeCoefficients, 0, syndromes.Coefficients.Length);

            GenericGFPoly forneySyndromes = new GenericGFPoly(field, syndromeCoefficients, false);

            for (int i = 0; i < positions.Length; i++)
            {
                int x = field.exp(positionsReversed[i]);
                for (int j = 0; j < forneySyndromes.Coefficients.Length - 1; j++)
                {
                    forneySyndromes.Coefficients[forneySyndromesLength - j - 1] = GenericGF.addOrSubtract(field.multiply(forneySyndromes.getCoefficient(j), x), forneySyndromes.getCoefficient(j + 1));
                }
            }

            return forneySyndromes;
        }

        // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        // this method replaces original method "runEuclideanAlgorithm"
        internal GenericGFPoly runBerlekampMasseyAlgorithm(GenericGFPoly syndrome, int erasureCount)
        {
            GenericGFPoly sigma = new GenericGFPoly(field, new int[] { 1 }, false);
            GenericGFPoly old = new GenericGFPoly(field, new int[] { 1 }, false);

            for (int i = 0; i < (syndrome.Coefficients.Length - erasureCount); i++)
            {
                int delta = syndrome.getCoefficient(i);
                for (int j = 1; j < sigma.Coefficients.Length; j++)
                {
                    delta ^= field.multiply(sigma.getCoefficient(j), syndrome.getCoefficient(i - j));
                }

                List<int> oldList = new List<int>(old.Coefficients);
                oldList.Add(0);
                old = new GenericGFPoly(field, oldList.ToArray(), false);

                if (delta != 0)
                {
                    if (old.Coefficients.Length > sigma.Coefficients.Length)
                    {
                        GenericGFPoly new_loc = old.multiply(delta);
                        old = sigma.multiply(field.inverse(delta));
                        sigma = new_loc;
                    }

                    sigma = sigma.addOrSubtract(old.multiply(delta));
                }
            }

            List<int> sigmaList = new List<int>(sigma.Coefficients);
            while (Convert.ToBoolean(sigmaList.Count) && sigmaList[0] == 0)
            {
                sigmaList.RemoveAt(0);
            }

            sigma = new GenericGFPoly(field, sigmaList.ToArray(), false);

            return sigma;
        }

        // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        private GenericGFPoly findErrataLocator(int[] errorPositions)
        {
            GenericGFPoly errataLocator = new GenericGFPoly(field, new int[] { 1 }, false);

            foreach (int i in errorPositions)
            {
                errataLocator = errataLocator.multiply(new GenericGFPoly(field, new int[] { 1 }, false).addOrSubtract(new GenericGFPoly(field, new int[] { field.exp(i), 0 }, false)));
            }

            return errataLocator;
        }

        // Method added by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        private GenericGFPoly? findErrorEvaluator(GenericGFPoly syndrome, GenericGFPoly errataLocations)
        {
            int[] product = syndrome.multiply(errataLocations).Coefficients;

            int[] target = new int[errataLocations.Coefficients.Length - 1];

            Array.Copy(product, product.Length - errataLocations.Coefficients.Length + 1, target, 0, target.Length);

            if (target.Length == 0)
            {
                return null;
            }

            GenericGFPoly omega = new GenericGFPoly(field, target, false);

            return omega;
        }

        private int[]? findErrorLocations(GenericGFPoly errorLocator)
        {
            // This is a direct application of Chien's search
            int numErrors = errorLocator.Degree;
            if (numErrors == 1)
            {
                // shortcut
                return new int[] { errorLocator.getCoefficient(1) };
            }
            int[] result = new int[numErrors];
            int e = 0;
            for (int i = 1; i < field.Size && e < numErrors; i++)
            {
                if (errorLocator.evaluateAt(i) == 0)
                {
                    result[e] = field.inverse(i);
                    e++;
                }
            }
            if (e != numErrors)
            {
                // throw new ReedSolomonException("Error locator degree does not match number of roots");
                return null;
            }
            return result;
        }

        // Method modified by Sonic-The-Hedgehog-LNK1123 (github.com/Sonic-The-Hedgehog-LNK1123)
        // added missing "if (denominator == 0)" check
        private int[]? findErrorMagnitudes(GenericGFPoly errorEvaluator, int[] errorLocations)
        {
            // This is directly applying Forney's Formula
            int s = errorLocations.Length;
            int[] result = new int[s];
            for (int i = 0; i < s; i++)
            {
                int xiInverse = field.inverse(errorLocations[i]);
                int denominator = 1;
                for (int j = 0; j < s; j++)
                {
                    if (i != j)
                    {
                        denominator = field.multiply(denominator, GenericGF.addOrSubtract(1, field.multiply(errorLocations[j], xiInverse)));
                    }
                }

                if (denominator == 0)
                {
                    return null;
                }

                result[i] = field.multiply(errorEvaluator.evaluateAt(xiInverse), field.inverse(denominator));
                if (field.GeneratorBase != 0)
                {
                    result[i] = field.multiply(result[i], xiInverse);
                }
            }
            return result;
        }
    }
}