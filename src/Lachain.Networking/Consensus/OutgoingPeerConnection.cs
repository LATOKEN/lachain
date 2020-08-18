using System;
using Lachain.Logger;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;
using Lachain.Utility.Benchmark;

namespace Lachain.Networking.Consensus
{
    public class OutgoingPeerConnection : IDisposable
    {
        private static readonly ILogger<OutgoingPeerConnection> Logger =
            LoggerFactory.GetLoggerForClass<OutgoingPeerConnection>();

        private readonly ThroughputCalculator _throughputCalculator;
        private readonly ClientWorker _client;

        public OutgoingPeerConnection(PeerAddress address, IMessageFactory messageFactory)
        {
            if (address.PublicKey is null) throw new Exception("Peer address must have public key");
            _throughputCalculator = new ThroughputCalculator(
                TimeSpan.FromSeconds(1),
                (speed, cnt) =>
                    Logger.LogTrace(
                        $"Outgoing bandwidth to peer {address}: {speed / 1024:0.00} KiB/s, {cnt} messages"
                    )
            );
            _client = new ClientWorker(address, address.PublicKey, messageFactory);
            _client.Start();
        }

        public void Send(NetworkMessage message)
        {
            _throughputCalculator.RegisterMeasurement(message.CalculateSize());
            _client.Send(message);
        }

        public void ReceiveAck(ulong messageId)
        {
            _client.ReceiveAck(messageId);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}