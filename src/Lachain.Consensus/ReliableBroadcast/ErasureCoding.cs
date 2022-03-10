using Lachain.Consensus.ReliableBroadcast.ReedSolomon.ReedSolomon;
using Lachain.Logger;
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Lachain.Consensus.ReliableBroadcast
{
    [DataContract]
    public class ErasureCoding
    {
        [DataMember]
        private static readonly ILogger<ErasureCoding> Logger = LoggerFactory.GetLoggerForClass<ErasureCoding>();

        [DataMember]
        private readonly GenericGF _field;
        
        public ErasureCoding()
        {
            _field = new GenericGF(285, 256, 0);
        }

        public void Encode(int[] plainData, int erasures)
        {
            var rse = new ReedSolomonEncoder(_field);
            rse.Encode(plainData, erasures);
        }

        public void Decode(int[] encryptionText, int additionalBits, int[] tips)
        {
            var rsd = new ReedSolomonDecoder(_field);
            if (rsd.Decode(encryptionText, additionalBits, tips)) return;
            Logger.LogError($"Too many errors-erasures to correct. Additional bits = {additionalBits}");
            Logger.LogError("Code: " + string.Join(", ", encryptionText));
            Logger.LogError("Tips: " + string.Join(", ", tips));
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(ErasureCoding));
            serializer.WriteObject(ms, this);

            return ms.ToArray();
        }

        public static ErasureCoding? FromBytes(ReadOnlyMemory<byte> bytes)
        {
            if(bytes.ToArray() == null)
            {
                return default;
            }

            using var memStream = new MemoryStream(bytes.ToArray());
            var serializer = new DataContractSerializer(typeof(ErasureCoding));
            var obj = (ErasureCoding?)serializer.ReadObject(memStream);

            return obj;
        }
    }
}