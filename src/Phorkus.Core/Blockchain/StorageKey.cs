using System;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain
{
    [Serializable]
    public class StorageKey
    {
        public UInt160 ScriptHash;

        public byte[] Key;
    }
}