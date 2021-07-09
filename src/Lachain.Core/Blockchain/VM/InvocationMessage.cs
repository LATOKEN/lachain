using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM
{
    class InvocationMessage
    {
        public UInt160 Sender { get; set; }
        public UInt256 Value { get; set; }
        public byte[] Sig { get; set; }
    }
}
