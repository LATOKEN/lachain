using System;

namespace NeoSharp.Core.Models
{
    /// <summary>
    /// 32 bit flag for transaction
    /// </summary>
    [Serializable]
    [Flags]
    public enum TransactionFlags : uint
    {
        None = 0x0000
    }
}