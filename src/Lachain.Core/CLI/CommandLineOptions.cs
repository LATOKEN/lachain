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
        
        [Option('c', "chainid", Required = true, HelpText = "ChainID for this network")]
        public ulong chainid { get; set; }

        [Option('d', "cycleDuration", Required = true, HelpText = "cycleDuration in blocks for this network")]
        public ulong cycleDuration { get; set; }
        
        [Option('v', "validatorCount", Required = true, HelpText = "expected validatorsCount for this network")]
        public ulong validatorsCount { get; set; }
        
        [Option('k', "network", Required = true, HelpText = "Name of the network")]
        public string networkName { get; set; }

        [Option('s', "stake", Required = false, HelpText = "Stake amount for initial validators set")]
        public string stakeAmount { get; set; } = "1000000";

        [Option('a', "feedAddress", Required = false, HelpText = "Feed address")]
        public string feedAddress { get; set; } = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";

        [Option('b', "feedBalance", Required = false, HelpText = "Initial feed balance")]
        public string feedBalance { get; set; } = "10000000";


        [Option('r', "hardfork", Required = true, Separator = ' ', HelpText = "hardfork heights")]
        public IEnumerable<ulong> hardforks { get; set; } = Enumerable.Empty<ulong>();


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

        [Option('s', "fastsync", Required = false, Separator = ' ', HelpText = "Performs fast-sync to a specific block on start")]
        public IEnumerable<string> SetStateTo { get; set; } = Enumerable.Empty<string>();
    }
    
    [Verb("db", HelpText = "cleanups in db")]
    public class DbOptions
    {
        [Option('c', "compact", Required = false, HelpText = "Compact Database")]
        public string? type { get; set; }
        // type should either be "soft" or "hard" 
        // "soft" -> only compact the db 
        // "hard" -> keep only recent snapshots. Be cautious before this clean-up. 
        //           Node will lose the ability to rollback and emulate in old snapshots and
        //           might lose some other ability as well
    }
}