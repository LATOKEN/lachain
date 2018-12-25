using Phorkus.Proto;

namespace Phorkus.Core.CLI
{
    public interface ICLICommands
    {
        SignedTransaction GetTransaction(string[] arguments);
        
        Block GetBlock(string[] arguments);
        
        UInt256 GetBalance(string[] arguments);

    }
}