using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Phorkus.Core.Network.Tcp
{
    public class TcpServer : IServer
    {
        public event EventHandler<IPeer> OnPeerConnected;
        public event EventHandler<IPeer> OnPeerClosed;

        private readonly TcpListener _tcpListener;
        private readonly ITransport _transport;
        private readonly NetworkConfig _networkConfig;

        public TcpServer(
            NetworkConfig networkConfig,
            ITransport transport)
        {
            if (networkConfig is null)
                throw new ArgumentException(nameof(networkConfig));
            _tcpListener = new TcpListener(IPAddress.Any, networkConfig.Port);
            _transport = transport ?? throw new ArgumentException(nameof(transport));
            _networkConfig = networkConfig;
        }
        
        public bool IsWorking { get; private set; }

        public void Start()
        {
            if (IsWorking)
                return;
            IsWorking = true;

            _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);

            _tcpListener.Stop();
            _tcpListener.Start();

            Task.Factory.StartNew(_Worker, TaskCreationOptions.LongRunning);
        }

        private void _Worker()
        {
            while (IsWorking)
            {
                Socket socket;
                try
                {
                    socket = _tcpListener.AcceptSocket();
                }
                catch (ObjectDisposedException)
                {
                    /* TODO: "log exception" */
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }
                var peer = new TcpPeer(socket, _transport);
                OnPeerConnected?.Invoke(this, peer);
            }
        }
        
        public void Stop()
        {
            IsWorking = false;
            _tcpListener.Stop();
        }

        public IPeer ConnectTo(IpEndPoint ipEndPoint)
        {
            var ipAddress = _GetIpAddress(ipEndPoint.Host);
            if (ipAddress == null)
                throw new InvalidOperationException($"\"{ipEndPoint.Host}\" cannot be resolved to an ip address.");

            if (_networkConfig.ForceIPv6)
                ipAddress = ipAddress.MapToIPv6();
            else if (ipAddress.IsIPv4MappedToIPv6)
                ipAddress = ipAddress.MapToIPv4();

            var ipEp = new IPEndPoint(ipAddress, ipEndPoint.Port);
//            _logger.LogInformation($"Connecting to {ipEp}...");
            
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            try
            {
                socket.Connect(ipEp.Address, ipEp.Port);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return null;
            }
            
//            _logger.LogInformation($"Connected to {ipEp}");
            var peer = new TcpPeer(socket, _transport);
            OnPeerConnected?.Invoke(this, peer);
            return peer;
        }
        
        /// <summary>
        /// Get Ip from hostname or address
        /// </summary>
        /// <param name="hostNameOrAddress">Host or Address</param>
        /// <returns>IpAdress</returns>
        private static IPAddress _GetIpAddress(string hostNameOrAddress)
        {
            if (IPAddress.TryParse(hostNameOrAddress, out var ipAddress))
                return ipAddress;
            try
            {
                var ipHostEntry = Dns.GetHostEntry(hostNameOrAddress);
                return ipHostEntry.AddressList
                    .FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            }
            catch
            {
                return null;
            }
        }
    }
}