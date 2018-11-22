﻿using System;
using System.Numerics;

namespace NeoSharp.VM
{
    public abstract class IIntegerStackItem : IStackItem<BigInteger>, IEquatable<IIntegerStackItem>
    {
        public override bool CanConvertToByteArray => true;

        public override byte[] ToByteArray() => Value.ToByteArray();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        protected IIntegerStackItem(BigInteger value) : base(value, EStackItemType.Integer) { }

        /// <summary>
        /// Is Equal
        /// </summary>
        /// <param name="other">Other</param>
        /// <returns>Return true if is equal</returns>
        public bool Equals(IIntegerStackItem other) => other != null && other.Value.Equals(Value);

        /// <summary>
        /// Is Equal
        /// </summary>
        /// <param name="other">Other</param>
        /// <returns>Return true if is equal</returns>
        public override bool Equals(IStackItem other)
        {
            if (other is IIntegerStackItem b)
            {
                return b.Value.Equals(Value);
            }

            return false;
        }
    }
}