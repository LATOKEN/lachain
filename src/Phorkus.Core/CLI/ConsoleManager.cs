using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.VM;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;

namespace Phorkus.Core.CLI
{
    public class ConsoleManager : IConsoleManager
    {
        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly IVirtualMachine _virtualMachine;
        private readonly ICrypto _crypto;
        private readonly ILogger<IConsoleManager> _logger;
        private IConsoleCommands _consoleCommands;

        public bool IsWorking { get; set; }

        public ConsoleManager(
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            IVirtualMachine virtualMachine,
            ICrypto crypto,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IStateManager stateManager,
            ILogger<IConsoleManager> logger)
        {
            _blockManager = blockManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
            _stateManager = stateManager;
            _logger = logger;
            _virtualMachine = virtualMachine;
        }

        private void _Worker(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            _consoleCommands = new ConsoleCommands(_transactionBuilder, _transactionPool,
                _transactionManager, _blockManager, _validatorManager, _stateManager, _virtualMachine,
                _crypto, keyPair);
            try
            {
                Console.Write(" > ");
                var command = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(command))
                    return;
                var argumentsTrash = command.Split(' ');
                var arguments = argumentsTrash.Where(argument => argument != " ").ToList();
                if (arguments.Count == 0)
                    return;
                var theCommand = _consoleCommands.GetType()
                    .GetMethods().FirstOrDefault(method => method.Name.ToLower().Contains(arguments[0].ToLower()));
                if (theCommand == null)
                    return;
                try
                {
                    var result = theCommand.Invoke(_consoleCommands, new object[] { arguments.ToArray() });
                    if (result == null)
                    {
                        _logger.LogError("Wrong arguments!\n");
                        Console.Out.Write("null\n");
                        return;
                    }
                    Console.Out.Write(result.ToString() + '\n');
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    _logger.LogError("Incorrect cli method call!\n");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                _logger.LogError("Incorrect cli method call!\n");
            }
        }

        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var thread = Thread.CurrentThread;
                    while (thread.IsAlive)
                    {
                        _Worker(thresholdKey, keyPair);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    _logger.LogError(e.Message);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            IsWorking = false;
        }
    }
}