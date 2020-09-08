using System;
using System.Text.RegularExpressions;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking
{
    public enum Protocol : byte
    {
        Unknown = 0,
        Tcp = 1,
        TcpWithTls = 2
    }
    
    public class PeerAddress : IEquatable<PeerAddress>
    {
        private static readonly Regex IpEndPointPattern =
            new Regex(@"^(?<proto>\w+)://(?<publicKey>(0x)?[0-9a-f]{66}@)?(?<address>[^/]+)/?:(?<port>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public Protocol Protocol { get; set; }

        public string? Host { get; set; }
        
        public int Port { get; set; }

        public ECDSAPublicKey? PublicKey { get; set; }

        public static PeerAddress Parse(string value)
        {
            var match = IpEndPointPattern.Match(value);
            if (!match.Groups["proto"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse protocol from URL (" + value + ")");
            if (!Enum.TryParse<Protocol>(match.Groups["proto"].ToString(), true, out var proto))
                throw new ArgumentOutOfRangeException(nameof(value), "Unable to resolve protocol specified (" + match.Groups["proto"] +") from URL");
            CheckAddress(value);
            var address = match.Groups["address"].ToString();
            if (!match.Groups["port"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse port from URL (" + value + ")");
            var port = match.Groups["port"].ToString();
            if (!match.Groups["publicKey"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse public key from URL (" + value + ")");
            var pk = match.Groups["publicKey"].ToString();
            if (pk.EndsWith("@"))
                pk = pk.Substring(0, pk.Length - 1);
            return new PeerAddress
            {
                Protocol = proto,
                Host = address,
                Port = int.Parse(port),
                PublicKey = pk.HexToBytes().ToPublicKey()
            };
        }

        public static void CheckAddress(string value)
        {
            var match = IpEndPointPattern.Match(value);
            if (!match.Groups["address"].Success)
                throw new ArgumentNullException(nameof(value), "Unable to parse address from URL (" + value + ")");
        }
        
        public override string ToString()
        {
            return $"{Protocol.ToString().ToLower()}://{PublicKey?.ToHex().Substring(2)}@{Host}:{Port}";
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Protocol;
                hashCode = (hashCode * 397) ^ (Host != null ? Host.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Port;
                return hashCode;
            }
        }

        public bool Equals(PeerAddress other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Protocol == other.Protocol && string.Equals(Host, other.Host) && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PeerAddress) obj);
        }
    }
}