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
            var heap = new C5.IntervalHeap<(NetworkMessagePriority, NetworkMessage)>(new NetworkMessageComparer());
            foreach (var item in lists)
            {
                heap.Add(item);
            }
            Assert.AreEqual(element * types.Count, heap.Count);

            types = types.OrderBy(type => (byte) type).ToList();
            for (int iter = 0; iter < types.Count; iter++)
            {
                Assert.AreEqual(iter, (byte) types[iter]);
                for (int i = 0; i < element; i++)
                {
                    var msg = heap.DeleteMin();
                    Assert.AreEqual(msg.Item1, types[iter]);
                }
            }
        }

        [Test]
        public void Test_DuplicateElement()
        {
            int element = 10;
            var heap = new C5.IntervalHeap<(NetworkMessagePriority, NetworkMessage)>(new NetworkMessageComparer());
            var types = Enum.GetValues(typeof(NetworkMessagePriority)).Cast<NetworkMessagePriority>().ToList();
            foreach (var type in types)
            {
                var msg = new NetworkMessage();
                for (int iter = 0; iter < element; iter++)
                {
                    heap.Add((type, msg));
                }
            }
            Assert.AreEqual(element * types.Count, heap.Count);
        }
    }
}