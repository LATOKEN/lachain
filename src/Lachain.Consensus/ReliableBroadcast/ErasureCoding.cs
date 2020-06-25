using Lachain.Consensus.ReliableBroadcast.ReedSolomon.ReedSolomon;
using Lachain.Logger;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ErasureCoding
    {
        private static readonly ILogger<ErasureCoding> Logger = LoggerFactory.GetLoggerForClass<ErasureCoding>();

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
    }
}