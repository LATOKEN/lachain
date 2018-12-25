using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;
using Phorkus.Storage.State;

namespace Phorkus.Core.CLI
{
    public class CLI : ICLI
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainStateManager _blockchainStateManager;
        private readonly ICrypto _crypto;
        private readonly ILogger<ICLI> _logger;
        private ICLICommands _cliCommands;

        public bool IsWorking { get; set; }

        public CLI(
            IGlobalRepository globalRepository,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            ICrypto crypto,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IBlockchainStateManager blockchainStateManager,
            ILogger<ICLI> logger)
        {
            _blockManager = blockManager;
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
            _blockchainStateManager = blockchainStateManager;
            _logger = logger;
        }

        private void _Worker(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            _cliCommands = new CLICommands(_globalRepository, _transactionBuilder, _transactionPool,
                _transactionManager, _blockManager, _validatorManager, _blockchainStateManager,
                keyPair);
            while (true)
            {
                try
                {
                    var command = Console.ReadLine();
                    if (command == null)
                        continue;
                    var argumentsTrash = command.Split(' ');
                    var arguments = argumentsTrash.Where(argument => argument != " ").ToList();
                    if (arguments.Count == 0)
                        continue;
                    var theCommand = _cliCommands.GetType().GetMethod(arguments[0]);
                    if (theCommand == null)
                        continue;
                    try
                    {
                        // ReSharper disable once CoVariantArrayConversion
                        var result = theCommand.Invoke(_cliCommands, arguments.ToArray());
                        if (result == null)
                        {
                            _logger.LogError("Wrong arguments!\n");
                            Console.Out.Write("null\n");
                        }
                        else
                        {
                            Console.Out.Write(result.ToString() + '\n');
                        }
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
                        Thread.Sleep(5000);
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