using System;
using System.Collections.Generic;
using System.Linq;
using STH1123.ReedSolomon;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ErasureCoding
    {
        private GenericGF _field; 
        
        private int _nPlayers;
        private int _nErasures;
        private int _nErrors;

        private int _dataSize;
        private int _additionalBits;
        
        public ErasureCoding(int additionalBits)
        {
            _field = new GenericGF(285, 256, 0);
            _dataSize = 0;
            _additionalBits = additionalBits;
        }
        
        public void  EncoderInPlace(int [] plainData)
        {
            
            var rse = new ReedSolomonEncoder(_field);
            
            rse.Encode(plainData, plainData.Length / 2);

        }
        
        public byte[] EncoderToByte(int [] plainData, int additionalInts)
        {
            _dataSize = plainData.Length;
            var countItems = _dataSize + additionalInts;
            var tmp = new int[countItems];
            for (var i = 0; i < _dataSize; i++)
            {
                tmp[i] = plainData[i];
            }
            for (var i = _dataSize; i < countItems; i++)
            {
                tmp[i] = 0;
            }
            var rse = new ReedSolomonEncoder(_field);
            
            rse.Encode(tmp, _dataSize);

            return tmp.Select(current => BitConverter.GetBytes(current)[0]).ToArray();
        }
        public int[] Encoder(int [] plainData, int additionalInts)
        {
            _dataSize = plainData.Length;
            var tmp = new int[_dataSize + additionalInts];
            for (var i = 0; i < _dataSize; i++)
            {
                tmp[i] = plainData[i];
            }
            for (var i = _dataSize; i < _dataSize + additionalInts; i++)
            {
                tmp[i] = 0;
            }
            var rse = new ReedSolomonEncoder(_field);
            
            rse.Encode(tmp, _dataSize);
            return tmp;
        }
        public void Decode(int [] encryptionText, int[] tips)
        {
            var rsd = new ReedSolomonDecoder(_field);
            
            if(rsd.Decode(encryptionText, _additionalBits, tips))
            {
                Console.WriteLine("Data corrected.");
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