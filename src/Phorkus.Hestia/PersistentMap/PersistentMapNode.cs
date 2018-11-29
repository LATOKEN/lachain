using System;
using System.Linq;

namespace Phorkus.Hestia.PersistentMap
{
    public class PersistentMapNode 
    {
        public ulong LeftSon { get; }
        public ulong RightSon { get; }
        public byte[] Key { get; }
        public byte[] Value { get; }
        public uint Size { get; }

        public PersistentMapNode(ulong leftSon, ulong rightSon, byte[] key, byte[] value, uint size)
        {
            LeftSon = leftSon;
            RightSon = rightSon;
            Key = key;
            Value = value;
            Size = size;
        }

        public static PersistentMapNode FromBytes(byte[] bytes)
        {
            var leftId = BitConverter.ToUInt64(bytes, 0);
            var rightId = BitConverter.ToUInt64(bytes, sizeof(ulong));
            var size = BitConverter.ToUInt32(bytes, 2 * sizeof(ulong));
            var len = BitConverter.ToInt32(bytes, 2 * sizeof(ulong) + sizeof(uint));
            var key = bytes.Skip(2 * sizeof(ulong) + sizeof(uint) + sizeof(int)).Take(len).ToArray();
            var value = bytes.Skip(2 * sizeof(ulong) + sizeof(uint) + sizeof(int) + len).ToArray();
            return new PersistentMapNode(leftId, rightId, key, value, size);
        }

        public byte[] ToByteArray()
        {
            var left = BitConverter.GetBytes(LeftSon);
            var right = BitConverter.GetBytes(RightSon);
            var size = BitConverter.GetBytes(Size);
            var keyLen = BitConverter.GetBytes(Key.Length);
            return left.Concat(right).Concat(size).Concat(keyLen).Concat(Key).Concat(Value).ToArray();
        }
    }
}