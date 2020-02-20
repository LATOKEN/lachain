using System;
using System.Collections.Generic;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;

namespace Phorkus.ConsensusTest
{
    public class InvokerId : IProtocolIdentifier
    {
        private static long _counter;
        public long Era => 0;
        public long Id { get; }

        public InvokerId()
        {
            Id = _counter++;
        }

        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Id);
        }

        public bool Equals(InvokerId other)
        {
            return Id == other.Id;
        }

        public override string ToString()
        {
            return $"Invoker {Id}";
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
            return Equals((InvokerId) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public class ProtocolInvoker<TId, TResult> : IConsensusProtocol where TId : IProtocolIdentifier
    {
        public IProtocolIdentifier Id { get; } = new InvokerId();

        public void Terminate()
        {
            throw new NotImplementedException();
        }

        public bool Terminated => false;

        public int ResultSet = 0;
        public TResult Result;

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