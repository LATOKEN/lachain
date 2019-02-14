using Phorkus.Utility;

namespace Phorkus.Core.CLI
{
    public interface IConsoleCommands
    {
        string GetTransaction(string[] arguments);

        string Help(string[] arguments);

        string GetBlock(string[] arguments);

        string SendTransaction(string[] arguments);

        string SendRawTransaction(string[] arguments);

        string SignTransaction(string[] arguments);

        string SignBlock(string[] arguments);
        
        Money GetBalance(string[] arguments);

        string DeployContract(string[] arguments);

        string CallContract(string[] arguments);        
    }
}