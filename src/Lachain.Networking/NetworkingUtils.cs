using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Lachain.Logger;

namespace Lachain.Networking
{
    public class NetworkingUtils
    {
        private static readonly ILogger<NetworkingUtils> Logger = LoggerFactory.GetLoggerForClass<NetworkingUtils>();

        public static bool IsSelfConnect(IPAddress ipAddress, string externalIp)
        {
            var localHost = new IPAddress(0x0100007f);
            if (ipAddress.Equals(localHost))
                return true;

            if (ipAddress.Equals(IPAddress.Parse(externalIp)))
                return true;

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                if (host.AddressList.Contains(ipAddress))
                    return true;
            }
            catch (Exception e)
            {
                Logger.LogWarning("Unable to GetHostName()");
            }

            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            return networkInterfaces
                .Where(ni =>
                    ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                )
                .Any(ni => ni.GetIPProperties()
                    .UnicastAddresses.Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Any(ip => ip.Address.Equals(ipAddress))
                );
        }
    }
}