﻿namespace Phorkus.WebAssembly
{
    /// <summary>
    /// Types for use as block signatures.
    /// </summary>
    public enum BlockType : sbyte
    {
        /// <summary>
        /// 32-bit integer value-type, equivalent to .NET's <see cref="int"/> and <see cref="uint"/>.
        /// </summary>
        Int32 = -0x01,
        /// <summary>
        /// 64-bit integer value-type, equivalent to .NET's <see cref="long"/> and <see cref="ulong"/>.
        /// </summary>
        Int64 = -0x02,
        /// <summary>
        /// 32-bit floating point value-type, equivalent to .NET's <see cref="float"/>.
        /// </summary>
        Float32 = -0x03,
        /// <summary>
        /// 64-bit floating point value-type, equivalent to .NET's <see cref="double"/>.
        /// </summary>
        Float64 = -0x04,
        /// <summary>
        /// Pseudo type for representing an empty block type.
        /// </summary>
        Empty = -0x40,
    }

    static class BlockTypeExtensions
    {
        public static bool TryToValueType(this BlockType blockType, out ValueType valueType)
        {
            switch (blockType)
            {
                default:
                case BlockType.Empty:
                    valueType = ValueType.Int32;
                    return false;
                case BlockType.Int32:
                    valueType = ValueType.Int32;
                    break;
                case BlockType.Int64:
                    valueType = ValueType.Int64;
                    break;
                case BlockType.Float32:
                    valueType = ValueType.Float32;
                    break;
                case BlockType.Float64:
                    valueType = ValueType.Float64;
                    break;
            }

            return true;
        }
    }
}