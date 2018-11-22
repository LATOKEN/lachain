using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Phorkus.Core.Logging;

namespace Phorkus.Core.Network.Tcp
{
    public class TcpServer : IServer
    {
        public event EventHandler<IPeer> OnPeerConnected;
        public event EventHandler<IPeer> OnPeerClosed;
        public event EventHandler<IPeer> OnPeerAccepted;

        private readonly TcpListener _tcpListener;
        private readonly ILogger<TcpServer> _logger;
        private readonly ITransport _transport;
        private readonly NetworkConfig _networkConfig;

        public TcpServer(
            NetworkConfig networkConfig,
            ITransport transport,
            ILogger<TcpServer> logger)
        {
            if (networkConfig is null)
                throw new ArgumentException(nameof(networkConfig));
            _tcpListener = new TcpListener(IPAddress.Any, networkConfig.Port);
            _transport = transport ?? throw new ArgumentException(nameof(transport));
            _networkConfig = networkConfig;
            _logger = logger;
        }

        public bool IsWorking { get; private set; }

        public void Start()
        {
            if (IsWorking)
                return;

            _logger.LogWarning("Starting TCP server");
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
                    _logger.LogWarning("TCP listener already disposed, exiting");
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }

                var peer = new TcpPeer(socket, _transport);
                OnPeerAccepted?.Invoke(this, peer);
            }
        }

        public void Stop()
        {
            IsWorking = false;
            _tcpListener.Stop();
        }

        private static bool _IsSelfConnect(IPAddress ipAddress)
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            if (host.AddressList.Contains(ipAddress))
                return true;
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }

                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (!ip.Address.Equals(ipAddress))
                        continue;
                    return true;
                }
            }

            return false;
        }

        public IPeer ConnectTo(IpEndPoint ipEndPoint)
        {
            var ipAddress = _GetIpAddress(ipEndPoint.Host);
            if (ipAddress == null)
                throw new InvalidOperationException($"\"{ipEndPoint.Host}\" cannot be resolved to an ip address.");

            if (_IsSelfConnect(ipAddress))
                return null;

            if (_networkConfig.ForceIPv6)
                ipAddress = ipAddress.MapToIPv6();
            else if (ipAddress.IsIPv4MappedToIPv6)
                ipAddress = ipAddress.MapToIPv4();

            var ipEp = new IPEndPoint(ipAddress, ipEndPoint.Port);
            _logger.LogInformation($"Connecting to {ipEp}...");

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            try
            {
                socket.Connect(ipEp.Address, ipEp.Port);
            }
            catch (Exception e)
            {
                _logger.LogTrace($"Unable to establish connection with client {ipEp}", e);
                return null;
            }

            _logger.LogInformation($"Connected to {ipEp}");
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