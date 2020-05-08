using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class RBTools
    {
        public static int[] ByteToInt(byte[] bytes)
        {
            const int limit = byte.MaxValue;
            var result = new int[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                if(bytes[i] <= limit)
                    result[i] = bytes[i];
            }
            return result;
        }
        
        public static byte[] IntToByte(int[] ints)
        {
            
            const int limit = byte.MaxValue;
            var result = new byte[ints.Length];
            for (var i = 0; i < ints.Length; i++)
            {
                if(ints[i] <= limit)
                    result[i] = BitConverter.GetBytes(ints[i])[0];
            }
            return result;
        }

        public static int[] GetOriginalInput(int[] correctInput)
        {
            var lenStore = BitConverter.ToInt32(IntToByte(correctInput.Take(4).ToArray()),0);
            return correctInput.Skip(4).Take(lenStore).ToArray();

        }
        
        public static List<int> GetCorrectInput(int[] input, int players)
        {
            if (players < 0) throw new Exception($"Something wrong: players in protocol - {players}");
            
            var len = input.Length;
            var lenInBytes = BitConverter.GetBytes(len);
            var cntBytes = lenInBytes.Length; // The count of bytes spend on a store of the size of a input
            var remainder = (len + cntBytes) % players;
            var additionalInt = players - remainder;
            var result = new List<int>();
            foreach (var partOfSize in ByteToInt(lenInBytes))
            {
                result.Add(partOfSize);
            }
            foreach (var item in input)
            {
                result.Add(item);
            }
            var rnd = new Random();
            for (int i = 0; i < additionalInt; i++)
            {
                result.Add(rnd.Next() % 255);
            }
            return result;
        }
    }
}