using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Console
{
    class Program
    {
        internal static void Main(string[] args)
        {
            var app = new Application();
            app.Start(args);
        }
        
        internal static void ZeroAttack(string[] args)
        {
            /* be very careful with this vulnerable shit: https://developers.google.com/protocol-buffers/docs/proto3#default */
            var test1 = new Test1
            {
                Value = 0x7b
            };
            var hex1 = test1.ToHash256().ToHex();
            /* !!! DON'T USE DEFAULT VALUES FOR MODIFIED STRUCTURE FIELDS AND CHECK STRUCTURE VERSION !!! */
            var test2 = new Test2
            {
                Value = 0x7b,
                Hello = 0x00
            };
            var hex2 = test2.ToHash256().ToHex();
            System.Console.WriteLine("HEX 1: " + hex1);
            System.Console.WriteLine("HEX 2: " + hex2);

            if (test2.Hello != 0)
            {
            }
        }
    }
}