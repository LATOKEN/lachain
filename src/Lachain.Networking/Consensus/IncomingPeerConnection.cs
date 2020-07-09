using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public event EventHandler<(MessageEnvelope envelope, ConsensusMessage message)>? OnMessage;

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
            NetworkMessage? message = null;
            try
            {
                message = NetworkMessage.Parser.ParseFrom(buffer);
            }
            catch (Exception e)
            {
                Logger.LogWarning(
                    $"Unable to parse protocol message from peer with public key {_publicKey.ToHex()}: {e}"
                );
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
            }

            if (message is null)
            {
                Logger.LogWarning($"Unable to parse protocol message from peer with public key {_publicKey.ToHex()}");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            if (message.MessageCase != NetworkMessage.MessageOneofCase.ConsensusMessage &&
                message.MessageCase != NetworkMessage.MessageOneofCase.Ack)
            {
                Logger.LogWarning(
                    $"Message of type {message.MessageCase} arrived from peer with public key {_publicKey.ToHex()}, skipping"
                );
                return;
            }

            var signedData = message.MessageCase == NetworkMessage.MessageOneofCase.ConsensusMessage
                ? message.ConsensusMessage.ToByteArray()
                : message.Ack.ToByteArray();
            if (!Crypto.VerifySignature(
                    signedData,
                    message.Signature.Encode(),
                    _publicKey.EncodeCompressed()
                )
            )
            {
                Logger.LogWarning(
                    $"Message with invalid signature arrived from peer with public key {_publicKey.ToHex()}, skipping"
                );
                return;
            }

            if (message.MessageCase == NetworkMessage.MessageOneofCase.Ack)
            {
                OnAck?.Invoke(this, (_publicKey, message.Ack.MessageId));
                return;
            }

            Logger.LogTrace($"Got message of type {message.MessageCase}");
            OnReceive?.Invoke(this, (_publicKey, message.MessageId));
            var envelope = new MessageEnvelope
            {
                PublicKey = _publicKey,
                Signature = message.Signature
            };
            OnMessage?.Invoke(this, (envelope, message.ConsensusMessage));
        }

        private void ProcessError(object sender, Exception error)
        {
            Logger.LogError($"Error handling message from peer with public key {_publicKey.ToHex()}: {error}");
        }

        private static byte[] SignatureData(IReadOnlyCollection<byte> message)
        {
            return Encoding.ASCII.GetBytes($"\x20LACHAIN Signed Network Message:\n{message.Count}")
                .Concat(message)
                .ToArray();
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}