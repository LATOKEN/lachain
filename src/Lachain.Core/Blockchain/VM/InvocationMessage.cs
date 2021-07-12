using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationMessage
    {
        public UInt160 Sender { get; set; }
        public UInt256 Value { get; set; }
    }
}
