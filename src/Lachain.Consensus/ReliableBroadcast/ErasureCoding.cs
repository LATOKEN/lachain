using System;
using System.Collections.Generic;
using Lachain.Consensus.ReliableBroadcast.ReedSolomon.ReedSolomon;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ErasureCoding
    {
        private GenericGF _field; 
        
        private int _nPlayers;
        private int _nErasures;
        private int _nErrors;

        private int _dataSize;
        private int _additionalBits;
        
        public ErasureCoding()
        {
            _field = new GenericGF(285, 256, 0);
            _dataSize = 0;
            _additionalBits = 0;
        }
        public void Encoder(int [] plainData)
        {
            _dataSize = plainData.Length;
            _additionalBits = _dataSize / 2 - 3;
            
            var rse = new ReedSolomonEncoder(_field);
            rse.Encode(plainData, _additionalBits);
        }
        public void Decode(int [] encryptionText, int[] tips)
        {
            var rsd = new ReedSolomonDecoder(_field);
            
            if(rsd.Decode(encryptionText, _additionalBits, tips))
            {
                Console.WriteLine("Data corrected.");
                //Console.WriteLine(String.Join(", ", afterRecieve.ToArray()));
            }
            else
            {
                Console.WriteLine("Too many errors-erasures to correct.");
            }
        }

        public void Print(String explanation, IEnumerable<int> source)
        {
            Console.WriteLine(explanation);
            String str;
            str = ""; 
            foreach (var elem in source)
            {
                str += elem + " ";
                    
            }
            Console.WriteLine(str);
        }
    }
}