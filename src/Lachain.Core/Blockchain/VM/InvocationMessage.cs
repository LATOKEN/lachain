using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationMessage
    {
        public UInt160 Sender { get; set; }
        public UInt256 Value { get; set; }
        public InvocationType Type { get; set; }
        public UInt160? Delegate { get; set; }

    }
}
