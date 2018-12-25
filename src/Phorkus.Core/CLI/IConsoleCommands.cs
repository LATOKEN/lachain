using Phorkus.Proto;

namespace Phorkus.Core.CLI
{
    public interface IConsoleCommands
    {
        SignedTransaction GetTransaction(string[] arguments);
        
        Block GetBlock(string[] arguments);
        
        UInt256 GetBalance(string[] arguments);

    }
}