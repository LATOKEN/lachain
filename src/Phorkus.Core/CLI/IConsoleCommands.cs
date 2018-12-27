using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.CLI
{
    public interface IConsoleCommands
    {
        SignedTransaction GetTransaction(string[] arguments);
        
        Block GetBlock(string[] arguments);
        
        string GetBalances(string[] arguments);
        
        Money GetBalance(string[] arguments);

    }
}