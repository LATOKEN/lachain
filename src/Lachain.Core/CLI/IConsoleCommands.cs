using Lachain.Utility;

namespace Lachain.Core.CLI
{
    public interface IConsoleCommands
    {
        string? GetTransaction(string[] arguments);

        string Help(string[] arguments);

        string? GetBlock(string[] arguments);

        string SendTransaction(string[] arguments);

        string? SendRawTransaction(string[] arguments);

        string SignTransaction(string[] arguments);

        Money? GetBalance(string[] arguments);

        string DeployContract(string[] arguments);

        string CallContract(string[] arguments);

        string SendContract(string[] arguments);

        string NewStake(string[] arguments);

        string ValidatorStatus(string[] arguments);

        string WithdrawStake(string[] arguments);

        string CurrentStake(string[] arguments);
    }
}