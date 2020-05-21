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

        
        public static List<int> GetCorrectInput(int[] input, int players, bool isRandom = true, int fill = 0)
        {   
            if (players < 0) throw new Exception($"Something wrong: players in protocol - {players}");
            if(input.Length == 0) return new List<int>();
            
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
            for (var i = 0; i < additionalInt; i++)
            {
                if (isRandom)
                {
                    result.Add(rnd.Next() % 255);    
                }
                else
                {
                    result.Add(fill);
                }
                
            }
            return result;
        }
        
        
        public static int[] GetCorrectInputWithoutSize(int[] input, int players)
        {   
            // In this version GetCorrectInput not add an information about a size of store in the start of the array
            if (players < 0) throw new Exception($"Something wrong: players in protocol - {players}");
            if(input.Length == 0) return new int[0];
            
            var extraNumbers = players - input.Length % players;
            var result = input.ToList();

            var rnd = new Random();
            for (var i = 0; i < extraNumbers; i++)
            {
                result.Add(rnd.Next() % 255);
            }
            return result.ToArray();
        }
        public static int[] GetOriginalInputWithoutSize(int[] correctInput, int extraNumbers)
        {
            return correctInput.Take(correctInput.Length - extraNumbers).ToArray();
        }        
        
        public static byte[] GetPermanentInput(int length)
        {
            var rnd = new Random();
            var input = new byte[length];
            for (var i = 0; i != length; i++)
            {
                input[i] = BitConverter.GetBytes((i + 10 + i*i) % 255)[0]; // for create permanent values
                
            }

            for (var i = length; i != length; i++)
                input[i] = 0;
            return input;
        }
        public static byte[] GetRandomInput(int length)
        {
            var rnd = new Random();
            var input = new byte[length];
            for (var i = 0; i != length; i++)
            {
                input[i] = BitConverter.GetBytes(rnd.Next() % 255)[0];
            }

            for (var i = length; i != length; i++)
                input[i] = 0;
            return input;
        }
        public static void Print(IEnumerable<int> source)
        {
            //Console.WriteLine(explanation);
            String str;
            str = source.Aggregate("", (current, elem) => current + (elem + " "));
            Console.WriteLine(str);
        }
    }
}