using System;
using System.Linq;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Nethereum.RLP;

namespace Lachain.Utility
{
    public class Contract
    {
        public Contract(UInt160 contractAddress, byte[] byteCode)
        {
            ContractAddress = contractAddress;
            ByteCode = byteCode;
        }

        public UInt160 ContractAddress { get; }
        public byte[] ByteCode { get; }

        public static Contract FromBytes(ReadOnlySpan<byte> bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            return new Contract(
                decoded[0].RLPData.ToUInt160(),
                decoded[1].RLPData
            );
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(ContractAddress.ToBytes().ToArray()),
                RLP.EncodeElement(ByteCode)
            );
        }
    }
}