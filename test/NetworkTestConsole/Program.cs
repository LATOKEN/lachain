using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog.LayoutRenderers;
using Phorkus.Crypto;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace NetworkTestConsole
{
    class Program : IMessageHandler
    {
        static void Main(string[] args)
        { 
            new Thread(() => _Thread("d95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48", 5050)).Start();
            new Thread(() => _Thread("8a04748ce6329cf899cee3f3e0f4720d1a6d917a9183a11b323315de2ffbf84d", 5051)).Start();

            while (true)
            {
                Thread.Sleep(1000);
            }
        } 
        
        private static void _Thread(string privateKey, ushort port)
        {
            var keyPair = new KeyPair(privateKey.HexToBytes().ToPrivateKey(), new BouncyCastle());
            var networkConfig = new NetworkConfig
            {
                Magic = 123,
                Port = port,
                Peers = new[]
                {
                    "tcp://192.168.88.154:5050@02affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5",
                    "tcp://192.168.88.154:5051@0252b662232efa6affe522a78fbe06df7bb5809db64a165cffa1dbb3154722389a"
                },
                ForceIPv6 = false,
                MaxPeers = 10
            };
            var prog = new Program();
            var networkManager = new NetworkManager(new BouncyCastle());
            var thradName = Thread.CurrentThread.ManagedThreadId;
            networkManager.OnClientConnected += peer =>
            {
                Console.WriteLine(thradName + ", " + port + ", connected: " + peer.Address);
            };
            networkManager.OnClientClosed += peer => Console.WriteLine(port + ", closed: " + peer.Address);
            networkManager.Start(networkConfig, keyPair, prog);
            while (true)
            {
                foreach (var peer in networkConfig.Peers)
                {
                    if (PeerAddress.Parse(peer).Port == port)
                        continue;
                    var client = networkManager.Connect(PeerAddress.Parse(peer));
                }
                Thread.Sleep(5000);
                break;
            }
        }

        public void PingRequest(MessageEnvelope envelope, PingRequest request)
        {
            throw new NotImplementedException();
        }

        public void PingReply(MessageEnvelope envelope, PingReply reply)
        {
            throw new NotImplementedException();
        }

        public void GetBlocksByHashesRequest(MessageEnvelope envelope, GetBlocksByHashesRequest request)
        {
            throw new NotImplementedException();
        }

        public void GetBlocksByHashesReply(MessageEnvelope envelope, GetBlocksByHashesReply reply)
        {
            throw new NotImplementedException();
        }

        public void GetBlocksByHeightRangeRequest(MessageEnvelope envelope, GetBlocksByHeightRangeRequest request)
        {
            throw new NotImplementedException();
        }

        public void GetBlocksByHeightRangeReply(MessageEnvelope envelope, GetBlocksByHeightRangeReply reply)
        {
            throw new NotImplementedException();
        }

        public void GetTransactionsByHashesRequest(MessageEnvelope envelope, GetTransactionsByHashesRequest request)
        {
            throw new NotImplementedException();
        }

        public void GetTransactionsByHashesReply(MessageEnvelope envelope, GetTransactionsByHashesReply reply)
        {
            throw new NotImplementedException();
        }

        public void ConsensusMessage(MessageEnvelope buildEnvelope, ConsensusMessage message)
        {
            throw new NotImplementedException();
        }
    }
}