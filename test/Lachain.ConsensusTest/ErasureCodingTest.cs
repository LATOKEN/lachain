using System;
using NUnit.Framework;
using Lachain.Consensus.ReliableBroadcast;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class ErasureCodingTest
    {
        private ErasureCoding _erasureCoding;

        private int[] _plainText;
        private int _countErrors;
        private int _countErasures;
        
        
        
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

            _erasureCoding = new ErasureCoding();
            _countErrors = 2;
            _countErasures = 3;
            _plainText = plainText;
        }

        [Test]
        public void TestEncoderDecoder()
        {
            //var plainText = GetInput(_nPlayers * _msgSize, _additionalInts);
            _erasureCoding.Print("Plain Text", _plainText);
            _erasureCoding.Encoder(_plainText);
            _erasureCoding.Print("After encoding", _plainText);
            
            // emulator  net ====================================================
            //var tips = new int[_countErasures + _countErrors];
            var tips = CorruptionNetwork(_plainText);
            _erasureCoding.Print("After corruption", _plainText);
            // ==================================================================
            
            _erasureCoding.Decode(_plainText, tips);
            _erasureCoding.Print("After decoding", _plainText);
            
            Console.WriteLine("Random Indexes");
            _erasureCoding.Print("After decoding", tips);
            
        }

        
        [Test]
        public void TestErasureCodingScheme()
        {
            
        }
        
        // The modelling of corruption of the network
        private int[] CorruptionNetwork(int[] sourceData)
        {
            var tips1 = GetRandomIndex(sourceData.Length, _countErasures + _countErrors);
            foreach (var randomIndex in tips1)
            {
                sourceData[randomIndex] = 0;
            }
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
                return new []{0};
            var charMax = range;
            var box = new int[charMax];
            var indexes = new int[count];
            for (var i = 0; i != charMax; i++)
            {
                box[i] = 1;
            }

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
    }
}






