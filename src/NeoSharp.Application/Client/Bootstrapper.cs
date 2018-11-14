using System;
using NeoSharp.Core;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Blockchain.Processing.BlockProcessing;
using NeoSharp.Core.DI;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Application.Client
{
    public class Bootstrapper : IBootstrapper
    {
        #region Variables

        /// <summary>
        /// Prompt
        /// </summary>
        private readonly IContainer _container;
        private readonly IPrompt _prompt;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="container"></param>
        /// <param name="prompt">Prompt</param>
        public Bootstrapper(
            IContainer container,
            IPrompt prompt)
        {
            _container = container;
            _prompt = prompt;
        }

        /// <summary>
        /// Run client with arguments
        /// </summary>
        /// <param name="args">Arguments</param>
        public void Start(string[] args)
        {            
            try
            {
                var transactionManager = _container.Resolve<ITransactionManager>();
                var assetRepository = _container.Resolve<IAssetRepository>();
                var blockchain = _container.Resolve<IBlockchain>();
                var blockProcessor = _container.Resolve<IBlockProcessor>();
                
                blockProcessor.Run();
                blockchain.InitializeBlockchain();
                
                var govAsset = assetRepository.GetAssetByName("LA");
                
                var tx = transactionManager.CreateContractTransaction(
                    from: UInt160.Parse("0x6bc32575acb8754886dc283c2c8ac54b1bd93195"),
                    to: UInt160.Parse("0x0000000000000000000000000000000000000000"),
                    asset: govAsset.Hash,
                    value: UInt256.FromDecimal(1)
                );
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(" ~ Startup exception ~");
                Console.Error.WriteLine("-------------------------------");
                Console.Error.WriteLine(e);
                Console.Error.WriteLine("-------------------------------");
                Environment.Exit(1);
            }
            
            Console.WriteLine("DONE");
            
//            transactionManager.CreateContractTransaction();

            //_prompt.StartPrompt(args);
        }
    }
}