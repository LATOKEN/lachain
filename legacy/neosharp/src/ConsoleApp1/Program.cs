using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        private static uint total = 0;
        
        static void Main(string[] args)
        {
            var sockets = new ConcurrentBag<Socket>();
            
            // server
            new Thread(() =>
            {
                var listener = new TcpListener(IPAddress.Any, 5050);
                listener.Start();
                while (true)
                {
                    var socket = listener.AcceptSocket();
                    sockets.Add(socket);
                }
            }).Start();
            
            // broadcaster
            new Thread(() =>
            {
                while (true)
                {
                    foreach (var s in sockets)
                    {
                        using (var stream = new MemoryStream())
                        using (var writer = new BinaryWriter(stream))
                        {
                            for (var i = 0; i < 256; i++)
                                writer.Write((byte) i);
                            writer.Flush();
                            var bytes = stream.ToArray();
                            s.Send(bytes);
                        }
                    }
                }
            }).Start();

            long lastTime = DateTime.Now.Millisecond;
            
            // client
            new Thread(() =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect("127.0.0.1", 5050);

                while (socket.Connected)
                {
                    var buffer = new byte[256];
                    var size = socket.Receive(buffer);
                    if (size == 0)
                        continue;
                    using (var stream = new MemoryStream(buffer))
                    using (var reader = new BinaryReader(stream))
                    {
                        for (var i = 0; i < 256; i++)
                        {
                            var item = reader.ReadByte();
                            if (item != i)
                            {
                                throw new Exception("Invalid item received");
                            }
                        }

                        total += 1024 * 4;
                    }
                }
            }).Start();
            
            while (true)
            {
                Thread.Sleep(1000);
                var curTime = DateTime.Now.Millisecond;
                var delTime = curTime - lastTime;
                if (delTime == 0)
                    continue;
                Console.WriteLine("BPS: " + 1000 * total / delTime / 1024 / 1024 + " mbyte/s");
                lastTime = curTime;
                total = 0;
            }
        }
    }
}