using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.UtilityTest
{
    public class IntervalHeapTest
    {
        [Test]
        public void Test_Sorting()
        {
            int element = 10;
            var lists = new List<(NetworkMessagePriority, NetworkMessage)>();
            var types = Enum.GetValues(typeof(NetworkMessagePriority)).Cast<NetworkMessagePriority>().ToList();
            foreach (var type in types)
            {
                for (int iter = 0; iter < element; iter++)
                {
                    var msg = new NetworkMessage();
                    lists.Add((type, msg));
                }
            }
            
            var rnd = new Random((int) TimeUtils.CurrentTimeMillis());
            lists = lists.OrderBy(_ => rnd.Next()).ToList();
            var heap = new C5.IntervalHeap<HubMessage>();
            foreach (var item in lists)
            {
                var msg = new HubMessage(item.Item1, item.Item2);
                heap.Add(msg);
            }
            Assert.AreEqual(element * types.Count, heap.Count);

            types = types.OrderBy(type => (byte) type).ToList();
            ulong prevTime = 0;
            int prevType = -1;
            for (int iter = 0; iter < types.Count; iter++)
            {
                Assert.AreEqual(iter, (byte) types[iter]);
                for (int i = 0; i < element; i++)
                {
                    var msg = heap.DeleteMin();
                    Assert.AreEqual(msg.Priority, types[iter]);
                    if (prevType == (int) msg.Priority)
                    {
                        Assert.That(prevTime <= msg.CreationTime);
                    }
                    prevType = (int) msg.Priority;
                    prevTime = msg.CreationTime;
                }
            }
        }

        [Test]
        public void Test_DuplicateElement()
        {
            int element = 10;
            var heap = new C5.IntervalHeap<HubMessage>();
            var types = Enum.GetValues(typeof(NetworkMessagePriority)).Cast<NetworkMessagePriority>().ToList();
            foreach (var type in types)
            {
                var msg = new NetworkMessage();
                var hubMsg = new HubMessage(type, msg);
                for (int iter = 0; iter < element; iter++)
                {
                    heap.Add(hubMsg);
                }
            }
            Assert.AreEqual(element * types.Count, heap.Count);
        }
    }
}