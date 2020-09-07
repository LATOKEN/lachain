using CommandLine;

namespace Lachain.Core.CLI
{
    [Verb("version", HelpText = "Print version and exit")]
    public class VersionOptions
    {
    }

    [Verb("decrypt", HelpText = "Decrypt wallet")]
    public class DecryptOptions
    {
        [Option('w', "wallet", Required = false, HelpText = "Path to wallet file")]
        public string WalletPath { get; set; } = "./wallet.json";

        [Option('p', "password", Required = true, HelpText = "Password")]
        public string WalletPassword { get; set; } = null!;
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

        [Option('p', "port", Required = false, HelpText = "Consensus port")]
        public ushort? ConsensusPort { get; set; }

        [Option('h', "host", Required = false, HelpText = "Consensus bind address")]
        public string? ConsensusHost { get; set; }
    }
}