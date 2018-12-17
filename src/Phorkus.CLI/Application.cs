using System;
using Phorkus.Core.DI;

namespace Phorkus.CLI
{
    public class Application : IBootstrapper
    {
        public Application()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, exception) =>
            {
                System.Console.Error.WriteLine(exception);
            };
        }

        public void Start(string[] args)
        {
            
        }
    }
}