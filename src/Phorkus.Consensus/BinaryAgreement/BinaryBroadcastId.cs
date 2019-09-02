﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcastId : IProtocolIdentifier
    {
        public BinaryBroadcastId(long era, long agreement, long epoch)
        {
            Era = era;
            Agreement = agreement;
            Epoch = epoch;
        }

        public long Era { get; }
        public long Agreement { get; }
        public long Epoch { get; }

        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era)
                .Concat(BitConverter.GetBytes(Agreement))
                .Concat(BitConverter.GetBytes(Epoch));
        }

        protected bool Equals(BinaryBroadcastId other)
        {
            return Era == other.Era && Agreement == other.Agreement && Epoch == other.Epoch;
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BinaryBroadcastId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Era.GetHashCode();
                hashCode = (hashCode * 397) ^ Agreement.GetHashCode();
                hashCode = (hashCode * 397) ^ Epoch.GetHashCode();
                return hashCode;
            }
        }
        
        public override string ToString()
        {
            return $"BB (Er={Era}, A={Agreement}, Ep={Epoch})";
        }
    }
}