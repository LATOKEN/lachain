using System;

namespace Phorkus.WebAssembly
{
    /// <summary>
    /// Describes the characteristics of an <see cref="OpCode"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OpCodeCharacteristicsAttribute : Attribute
    {
        /// <summary>
        /// The standardized name for the opcode.  Cannot be null.
        /// </summary>
        public string Name { get; }

        public uint Gas { get; }

        public bool IsFlowControl { get; set; } = false;

        //It may be useful to track other characteristics here in the future.

        /// <summary>
        /// Creates a new <see cref="OpCodeCharacteristicsAttribute"/> with the provided characteristics.
        /// </summary>
        /// <param name="name">The standardized name for the opcode.  Cannot be null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> cannot be null.</exception>
        public OpCodeCharacteristicsAttribute(string name, uint gas = 1)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Gas = gas;
        }
    }
}