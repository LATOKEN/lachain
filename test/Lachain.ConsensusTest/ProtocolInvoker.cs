using System;
using System.Collections.Generic;
using Lachain.Consensus;
using Lachain.Consensus.Messages;

namespace Lachain.ConsensusTest
{
    public class InvokerId : IProtocolIdentifier
    {
        private static long _counter;

        public InvokerId()
        {
            Id = _counter++;
        }

        public long Id { get; }
        public long Era => 0;

        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Id);
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public bool Equals(InvokerId other)
        {
            return Id == other.Id;
        }

        public override string ToString()
        {
            return $"Invoker {Id}";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InvokerId) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public class ProtocolInvoker<TId, TResult> : IConsensusProtocol where TId : IProtocolIdentifier
    {
        public TResult Result;

        public int ResultSet;
        public IProtocolIdentifier Id { get; } = new InvokerId();

        public void Terminate()
        {
            throw new NotImplementedException();
        }

        public bool Terminated => false;

        public void ReceiveMessage(MessageEnvelope message)
        {
            if (message.External || !(message.InternalMessage is ProtocolResult<TId, TResult> result)) return;
            //Console.Error.WriteLine($"{Id}: got result from {result.From}");
            ResultSet++;
            Result = result.Result;
        }

        public void Start()
        {
        }

        public void WaitFinish()
        {
        }

        public void WaitResult()
        {
            throw new NotImplementedException();
        }
    }
}