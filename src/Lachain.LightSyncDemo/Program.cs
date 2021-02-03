using Lachain.Core.CLI;

namespace Lachain.LightSyncDemo
{
    class Program
    {
        static void Main()
        {
            var options = new RunOptions
            {
                LogLevel = "Trace"
            };
            using var app = new Application(options);
            app.Start(options);
        }
    }
}