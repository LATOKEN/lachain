using Phorkus.Crypto;
using Phorkus.Network.Grpc;

namespace Phorkus.Core
{
    public interface IThresholdService
    {
        ThresholdMessage ExchangeMessage(ThresholdMessage thresholdMessage, KeyPair keyPair);
    }
}