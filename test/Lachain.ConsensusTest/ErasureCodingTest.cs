using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus.ReliableBroadcast;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class ErasureCodingTest
    {
        [SetUp]
        public void SetUp()
        {
            // inputs -------------------------------
            var faulty = 3; // faulty = errors + erasures
            var players = 8;
            var msgSize = 2;
            var additionalInts = players;
            var plainText = GetInput(players * msgSize, additionalInts);
            // --------------------------------------

            _erasureCoding = new ErasureCoding(additionalInts);
            _countErrors = 2;
            _countErasures = 3;
            _plainText = plainText;
        }

        private ErasureCoding _erasureCoding;

        private int[] _plainText;
        private int _countErrors;
        private int _countErasures;

        // The modelling of corruption of the network
        private int[] CorruptionNetworkInPlace(int[] sourceData, int countErasures, int countErrors)
        {
            var tips1 = GetPermanentIndex( countErasures + countErrors);
            //var tips1 = GetRandomIndex(sourceData.Length, countErasures + countErrors);
            foreach (var randomIndex in tips1) 
                sourceData[randomIndex] = 0;
            //_erasureCoding.Print(GetRandomIndex(data.Length, countErasures + countErrors));
            return tips1;
        }

        private int[] GetInput(int length, int additionalInts)
        {
            var summaryLength = length + additionalInts;
            var rnd = new Random();
            var input = new int[summaryLength];
            for (var i = 0; i != length; i++)
                input[i] = rnd.Next() % 255;

            for (var i = length; i != summaryLength; i++)
                input[i] = 0;
            return input;
        }

        private int[] GetRandomIndex(int range, int count)
        {
            if (range > 255)
                return new[] {0};
            var charMax = range;
            var box = new int[charMax];
            var indexes = new int[count];
            for (var i = 0; i != charMax; i++) box[i] = 1;

            var countTry = 0;
            var rnd = new Random();
            while (countTry < count)
            {
                var currentTry = rnd.Next(range - 1);
                if (box[currentTry] != 1) continue;
                indexes[countTry] = currentTry;
                countTry++;
            }

            return indexes;
        }
        private int[] GetPermanentIndex(int count)
        {
            var res = new int[count];
            for (int i = 0; i < count; i++)
            {
                res[i] = i + 1;
            }
            return res;
        }

        [Test]
        public void TestEncoderDecoder()
        {   
            //var plainText = GetInput(_nPlayers * _msgSize, _additionalInts);
            _erasureCoding.Print("Plain Text", _plainText);
            _erasureCoding.EncoderInPlace(_plainText);
            _erasureCoding.Print("After encoding", _plainText);

            // emulator  net ====================================================
            //var tips = new int[_countErasures + _countErrors];
            var tips = CorruptionNetworkInPlace(_plainText, _countErasures, _countErrors);
            _erasureCoding.Print("After corruption", _plainText);
            // ==================================================================

            _erasureCoding.Decode(_plainText, tips);
            _erasureCoding.Print("After decoding", _plainText);

            Console.WriteLine("Random Indexes");
            _erasureCoding.Print("After decoding", tips);
        }
        
        
        
        public bool Pass(int storeSz, int cntError)
        {
            var flag = true; 
            var nPlayers = storeSz;
            var msgSize = 1;
            var countErasure = cntError;
            var countError = 0;
            var storeLen = nPlayers * msgSize;
            var additionalInts = storeLen;
            
            var plainText = GetInput(storeLen, additionalInts);
            
            if(flag)_erasureCoding.Print("Plain Text", plainText);
            var afterEncode = _erasureCoding.EncoderInPlaceNew(plainText, additionalInts);
            if(flag)_erasureCoding.Print("After encoding", afterEncode);

            // emulator  net ====================================================
            //var tips = new int[_countErasures + _countErrors];
            var tips = CorruptionNetworkInPlace(afterEncode, countErasure, countError);
            if(flag)_erasureCoding.Print("After corruption", afterEncode);
            // ==================================================================

            _erasureCoding.Decode(afterEncode, null);
            
            if(flag)_erasureCoding.Print("Random Indexes", tips);
            if(flag)_erasureCoding.Print("Plain Text  :", plainText);
            if(flag)_erasureCoding.Print("After Decode:", afterEncode);
            return plainText.Take(nPlayers).SequenceEqual(afterEncode.Take(nPlayers));
        }
        [Test]
        [Repeat(50)]
        public void TestThird1()
        {
                
                Console.WriteLine(Pass(30, 5) ?
                    $" ================ PASS ================ ": $" ================ NOT PASS ================ ");

        }
        [Test]
        public void TestThird()
        {
            for (int StoreSz = 22; StoreSz < 40; StoreSz++)
            {
                for (var i = 0; i < 15; i++)
                {
                    //Console.WriteLine(Pass(StoreSz, i) ? $"PASS stSz: {StoreSz} ecc: {i}": $"NOT PASS stSz: {StoreSz} ecc: {i}");
                    Console.WriteLine(Pass(StoreSz, i) ? $"PASS stSz: {StoreSz} ecc: {i}": "");
                }    
            }
            
        }
        
        
        [Test]
        public void TestSimpleFlowRBC()
        {
            var lPeice = 22;
            var N = 22;
            var lenBigInput = lPeice * N;
            var coeffAdditionalPositions = 1;
            var additionalPositions = lPeice * coeffAdditionalPositions;
            
            var pt = GetInput(lenBigInput, additionalPositions);
            
            var ecBigInput = new ErasureCoding(additionalPositions);
            
            var Gs = new List<Array>();
            for (var i = 0; i < pt.Length / lPeice; i++)
            {
                var peice = pt.Skip(i * lPeice).Take(lPeice).ToArray();
                var G = ecBigInput.Encoder(peice, additionalPositions).ToArray();
                Gs.Add(G);
            }
            
            // prepare batches for players
            // =========================================================================================================
            var batches = new List<List<int[]>>();
            for (int i = 0; i < N; i++)
            {
                batches.Add(new List<int[]>());
            }
            
            var segment = Gs[0].Length / N;
            foreach (var arr in Gs)
            {
                for (var i = 0; i < N; i++)
                {
                    var tmp = new int[segment];
                    Array.Copy(arr, i * segment, tmp, 0, segment);
                    
                    batches[i].Add(tmp);
                }
            }
            // =========================================================================================================

            // there is a model of a decoding process when was received needed a count of pieces of the message
            var result = new int[pt.Length];
            var R = batches[0].Count;
            var toDecode = new int[segment * N];

            var tips = new []{0, 7, 6, 15,18,4}; // the array contains indexes each of them should less than the count of players
            for (var i = 0; i < R; i++)
            {
                for (var j = 0; j < N; j++)
                {
                    if (j == tips[0])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    } 
                    else if (j == tips[1])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    }
                    else if (j == tips[2])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    }
                    else if (j == tips[3])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    }
                    else if (j == tips[4])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    }
                    else if (j == tips[5])
                    {
                        Array.Copy(new int[segment], 0, toDecode, j * segment,segment);
                    }
                    else
                    {
                        //var zeros = new int[segment];
                        Array.Copy(batches[j][i], 0, toDecode, j * segment,segment);
                    }
                }
                var ecBigInput1 = new ErasureCoding(additionalPositions);
                ecBigInput1.Decode(toDecode, null);
                
                var correctData = toDecode.Length / (coeffAdditionalPositions + 1);
                Array.Copy(toDecode, 0, result, i * correctData, correctData);
            }

            if(pt.SequenceEqual(result))
            {
                Console.WriteLine("Test passed");
                // _erasureCoding.Print("bigPlainText: ", bigPlainText);
                // _erasureCoding.Print("result: ", result);
            }
            else
            {
                Console.WriteLine("Test not passed");
                // _erasureCoding.Print("bigPlainText: ", bigPlainText);
                // _erasureCoding.Print("result: ", result);
            }
        }
    }
}