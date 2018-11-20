using System;
using System.Text.RegularExpressions;

namespace Phorkus.Core.Network
{
    public class IpEndPoint
    {
        private static readonly Regex IpEndPointPattern =
            new Regex(@"^(?<proto>\w+)://(?<address>[^/]+)/?:(?<port>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public Protocol Protocol { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public static IpEndPoint Parse(string value)
        {
            var match = IpEndPointPattern.Match(value);
            if (!match.Groups["proto"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse protocol from URL (" + value + ")");
            if (!Enum.TryParse<Protocol>(match.Groups["proto"].ToString(), true, out var proto))
                throw new ArgumentOutOfRangeException(nameof(value), "Unable to resolve protocol specified (" + match.Groups["proto"] +") from URL");
            if (!match.Groups["address"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse address from URL (" + value + ")");
            var address = match.Groups["address"].ToString();
            if (!match.Groups["port"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse port from URL (" + value + ")");
            var port = match.Groups["port"].ToString();
            return new IpEndPoint
            {
                Protocol = proto,
                Host = address,
                Port = int.Parse(port)
            };
        }

        public override string ToString()
        {
            return $"{Protocol.ToString().ToLower()}://{Host}:{Port}";
        }

        public override int GetHashCode()
        {
            var hashCode = Protocol.GetHashCode();
            hashCode ^= Host.GetHashCode();
            hashCode ^= Port.GetHashCode();
            return hashCode;
        }
    }
}