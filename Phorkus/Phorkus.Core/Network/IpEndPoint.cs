using System.Text.RegularExpressions;

namespace Phorkus.Core.Network
{
    public class IpEndPoint
    {
        private static readonly Regex IpEndPointPattern =
            new Regex(@"^(?<proto>\w+)://(?<address>[^/]+)/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public Protocol Protocol { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }
    }
}