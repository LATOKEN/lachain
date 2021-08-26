using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace Lachain.Core.CLI
{
    [Verb("decrypt", HelpText = "Decrypt wallet")]
    public class DecryptOptions
    {
        [Option('w', "wallet", Required = false, HelpText = "Path to wallet file")]
        public string WalletPath { get; set; } = "./wallet.json";

        [Option('p', "password", Required = true, HelpText = "Password")]
        public string WalletPassword { get; set; } = null!;
    }
    
    [Verb("encrypt", HelpText = "Encrypt wallet")]
    public class EncryptOptions
    {
        [Option('w', "wallet", Required = false, HelpText = "Path to wallet file")]
        public string WalletPath { get; set; } = "./wallet.json";

        [Option('p', "password", Required = true, HelpText = "Password")]
        public string WalletPassword { get; set; } = null!;
    }
    
    [Verb("keygen", HelpText = "Run trusted threshold keygen")]
    public class KeygenOptions
    {
        [Option('i', "ips", Required = false, Separator = ' ', HelpText = "IP addresses for config generation, local testnet with 127.0.0.1 address is used if missed")]
        public IEnumerable<string> IpAddresses { get; set; } = Enumerable.Empty<string>();

        [Option('n', "players", Required = true, HelpText = "Total number of participants")]
        public int N { get; set; }
        
        [Option('f', "faulty", Required = true, HelpText = "Total number of faulty participants")]
        public int F { get; set; }
        
        [Option('p', "port", Required = false, HelpText = "RPC port for nodes or base port for local installation, default is 7070")]
        public ushort port { get; set; }
        
        [Option('t', "target", Required = false, HelpText = "Block target time in ms, default is 5000 (5sec)")]
        public ushort target { get; set; }
    }

    [Verb("run", isDefault: true, HelpText = "Run the node")]
    public class RunOptions
    {
        [Option('c', "config", Required = false, HelpText = "Path to config file")]
        public string ConfigPath { get; set; } = "./config.json";

        [Option('w', "wallet", Required = false, HelpText = "Path to wallet file")]
        public string? WalletPath { get; set; }

        [Option('l', "log", Required = false, HelpText = "Log level: TRACE, DEBUG, INFO, WARN, ERROR")]
        public string? LogLevel { get; set; }

        [Option('d', "datadir", Required = false, HelpText = "Directory for chain data")]
        public string? DataDir { get; set; }

        [Option('z', "rpcaddr", Required = false, HelpText = "RPC node bind address")]
        public string? RpcAddress { get; set; }

        [Option('x', "rpcport", Required = false, HelpText = "RPC node port")]
        public ushort? RpcPort { get; set; }

        [Option('a', "apikey", Required = false, HelpText = "Api key for RPC")]
        public string? RpcApiKey { get; set; }
        
        [Option('b', "rollbackto", Required = false, HelpText = "Rollback node to specific block on start")]
        public ulong? RollBackTo { get; set; }

        [Option('s', "setstateto", Required = false, Separator = ' ', HelpText = "Set state node to a specific block on start")]
        public IEnumerable<string> SetStateTo { get; set; } = Enumerable.Empty<string>();
    }
}