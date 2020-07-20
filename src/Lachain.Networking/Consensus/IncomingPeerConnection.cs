using System;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Consensus
{
    public class IncomingPeerConnection : IDisposable
    {
        private static readonly ILogger<IncomingPeerConnection> Logger =
            LoggerFactory.GetLoggerForClass<IncomingPeerConnection>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public readonly int Port;
        private readonly ECDSAPublicKey _publicKey;
        private readonly ServerWorker _server;

        public event EventHandler<(ECDSAPublicKey publicKey, ulong messageId)>? OnReceive;
        public event EventHandler<(ECDSAPublicKey publicKey, ulong messageId)>? OnAck;
        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnMessage;

        public IncomingPeerConnection(string bindAddress, ECDSAPublicKey publicKey)
        {
            _server = new ServerWorker(bindAddress);
            _publicKey = publicKey;
            Port = _server.Port;
            _server.OnMessage += ProcessMessage;
            _server.OnError += ProcessError;
            _server.Start();
        }

        private void ProcessMessage(object sender, byte[] buffer)
        {
            // TODO: can we also check origin of message?
            MessageBatch? messages = null;
            try
            {
                messages = MessageBatch.Parser.ParseFrom(buffer);
            }
            catch (Exception e)
            {
                Logger.LogWarning(
                    $"Unable to parse protocol message from peer with public key {_publicKey.ToHex()}: {e}"
                );
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
            }

            if (messages is null)
            {
                Logger.LogWarning($"Unable to parse protocol message from peer with public key {_publicKey.ToHex()}");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            if (!Crypto.VerifySignature(
                messages.Content.ToByteArray(),
                messages.Signature.Encode(),
                _publicKey.EncodeCompressed())
            )
            {
                Logger.LogWarning(
                    $"Messages with invalid signature arrived from peer with public key {_publicKey.ToHex()}, skipping"
                );
                return;
            }

            OnReceive?.Invoke(this, (_publicKey, messages.MessageId));

            foreach (var message in messages.Content.Messages)
            {
                if (message.MessageCase != NetworkMessage.MessageOneofCase.ConsensusMessage &&
                    message.MessageCase != NetworkMessage.MessageOneofCase.Ack)
                {
                    Logger.LogWarning(
                        $"Message of type {message.MessageCase} arrived from peer with public key {_publicKey.ToHex()}, skipping"
                    );
                    continue;
                }

                if (message.MessageCase == NetworkMessage.MessageOneofCase.Ack)
                {
                    OnAck?.Invoke(this, (_publicKey, message.Ack.MessageId));
                    continue;
                }

                OnMessage?.Invoke(this, (message.ConsensusMessage, _publicKey));
            }
        }

        private void ProcessError(object sender, Exception error)
        {
            Logger.LogError($"Error handling message from peer with public key {_publicKey.ToHex()}: {error}");
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}